using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows.Forms;
using RTS.Models;

namespace RTS.Services
{
    // Egy modul frissitesi allapota, amit a CheckForUpdates ad vissza -
    // ez jeleníti meg a Home/betoltokepernyon es az InfoView-n, hogy hany
    // modulhoz van ujabb valtozat.
    public class ModuleUpdateStatus
    {
        public string Name { get; set; } = "";
        public bool Installed { get; set; }
        public bool HasUpdate { get; set; }
        public string? LocalCommit { get; set; }
        public string? RemoteCommit { get; set; }
        public DateTime? RemoteCommitDate { get; set; }
        public string? Error { get; set; }
    }

    // Elso-inditasi telepito: ha az RTS.exe maga mellett nem talal modules.json-t,
    // megkerdezi hova telepitse az adatokat (alapertelmezes: C:\Program Files\RTS),
    // letolti a modules.json-t es minden engedelyezett, publikus modult, majd
    // elmenti a valasztott mappat, hogy legkozelebb ne kelljen ujra kerdezni.
    //
    // Verzio v0.4.0 - 2026-09-11: COMMIT-ALAPU FRISSITES-ELLENORZES bevezetve.
    // Korabban egy mar letezo modul-mappat MINDIG kihagyott a telepito/
    // ujratelepito - sosem nezte meg, hogy kozben frissult-e a repo a
    // GitHub-on. Ez okozta, hogy a SetUpER 9. korben pusholt javitasai
    // (ekezet-hiba, uj telepitok) sosem jutottak el egy mar telepitett
    // peldanyhoz, meg az "Ujratelepites/ellenorzes" gombra kattintva sem.
    // Mostantol minden telepitett modul mellett egy ".rts-installed.json"
    // fajl tarolja, PONTOSAN melyik commit lett letoltve - ez hasonlitodik
    // ossze a GitHub-on levo legfrissebb commit-tal.
    public static class RtsInstaller
    {
        private const string GithubRawModulesUrl = "https://raw.githubusercontent.com/LordAthis/RTS/main/modules.json";
        private const string ConfigDir = "RTS";
        private const string ConfigFile = "install.json";
        private const string InstallMetaFile = ".rts-installed.json";

        private static string ConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ConfigDir, ConfigFile);

        // A korabban elmentett adat-mappa beolvasasa, ha van es ervenyes
        // (letezik benne modules.json).
        public static string? ReadSavedRoot()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return null;
                string json = File.ReadAllText(ConfigPath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("dataRoot", out var el))
                {
                    string? path = el.GetString();
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "modules.json")))
                        return path;
                }
            }
            catch { /* serult/hianyzo config - ugy kezeljuk mintha nem lenne */ }
            return null;
        }

        private static void SaveRoot(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new { dataRoot = path }));
            }
            catch { /* nem vegzetes - legfeljebb ujra megkerdezzuk legkozelebb */ }
        }

        // Felugro mappavalaszto, alapertelmezett javaslattal.
        public static string? PromptForInstallFolder()
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "Valaszd ki, hova telepitse az RTS a moduljait (ajanlott: C:\\Program Files\\RTS)",
                UseDescriptionForTitle = true,
                SelectedPath = @"C:\Program Files\RTS"
            };

            var result = dlg.ShowDialog();
            if (result != DialogResult.OK) return null;

            return string.IsNullOrWhiteSpace(dlg.SelectedPath) ? @"C:\Program Files\RTS" : dlg.SelectedPath;
        }

        // Teljes elso-telepites: modules.json letoltese + minden engedelyezett,
        // publikus modul letoltese. A log delegate-en keresztul folyamatosan
        // visszajelez, hogy a hivo (UI szal) meg tudja jeleniteni.
        public static void RunFirstTimeSetup(string rootPath, Action<string> log)
        {
            Directory.CreateDirectory(rootPath);
            string appsDir = Path.Combine(rootPath, "Apps");
            Directory.CreateDirectory(appsDir);

            log($"modules.json letoltese: {GithubRawModulesUrl}");
            using var http = new HttpClient();
            string json;
            try
            {
                json = http.GetStringAsync(GithubRawModulesUrl).GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(rootPath, "modules.json"), json);
                log("modules.json letoltve.");
            }
            catch (Exception ex)
            {
                log($"HIBA: modules.json letoltese sikertelen - {ex.Message}");
                return;
            }

            // Mentjuk a valasztott mappat, meg mielott a modulok letoltese
            // elkezdodne - ha kozben valami elszall, legalabb a modules.json
            // mar megvan es legkozelebb nem kerdez ujra.
            SaveRoot(rootPath);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var modules = JsonSerializer.Deserialize<List<ModuleInfo>>(json, options) ?? new List<ModuleInfo>();

            bool gitAvailable = IsGitAvailable();
            log(gitAvailable ? "Git elerheto - klonozassal dolgozunk." : "Git nem elerheto - ZIP letoltesre valtunk.");

            foreach (var mod in modules)
            {
                if (!mod.Enabled)
                {
                    log($"[{mod.Name}] Letiltva - kihagyva.");
                    continue;
                }
                if (mod.Visibility == "private")
                {
                    log($"[{mod.Name}] Premium/zart modul - a grafikus telepito ezt meg nem kezeli. Futtasd kezzel a bootstrap.ps1-et -LicenseToken paraméterrel.");
                    continue;
                }

                string targetPath = Path.Combine(appsDir, mod.Name);
                if (Directory.Exists(targetPath))
                {
                    // MAR NEM feltetel nelkul kihagyjuk - megnezzuk, van-e
                    // ujabb commit a GitHub-on a helyben tarolt .rts-installed.json
                    // alapjan, es ha igen (vagy ha meg nincs meta - regi,
                    // e funkcio elotti telepites), ujratoltjuk a modult.
                    var localMeta = ReadInstallMeta(targetPath);
                    var (remoteSha, remoteDate, remoteErr) = GetLatestRemoteCommit(mod.Repo);

                    if (remoteErr != null)
                    {
                        log($"[{mod.Name}] Mar letezik - frissites-ellenorzes sikertelen ({remoteErr}), a meglevo peldany marad.");
                        continue;
                    }

                    bool needsUpdate = localMeta == null || !string.Equals(localMeta.Commit, remoteSha, StringComparison.OrdinalIgnoreCase);
                    if (!needsUpdate)
                    {
                        log($"[{mod.Name}] Mar letezik es naprakesz (commit: {ShortSha(remoteSha)}).");
                        continue;
                    }

                    log($"[{mod.Name}] Frissites elerheto (helyi: {(localMeta == null ? "ismeretlen (regi telepites)" : ShortSha(localMeta.Commit))} -> uj: {ShortSha(remoteSha)}, {remoteDate:yyyy-MM-dd}) - ujratoltes...");
                    try { Directory.Delete(targetPath, true); }
                    catch (Exception ex)
                    {
                        log($"[{mod.Name}] HIBA: a regi mappa torlese sikertelen ({ex.Message}) - a frissites kihagyva, kezzel torolheted: {targetPath}");
                        continue;
                    }
                }

                log($"[{mod.Name}] Telepites: {mod.Repo}");
                bool ok = gitAvailable && CloneViaGit(mod.Repo, targetPath, log);
                string method = ok ? "git" : "";
                if (!ok) { ok = DownloadZip(mod.Repo, targetPath, mod.Name, log); method = ok ? "zip" : ""; }
                if (ok)
                {
                    CleanupNonWindowsDirs(targetPath, mod.Name, log);
                    SaveInstallMetaAfterInstall(targetPath, mod.Repo, method, log);
                }
                log(ok ? $"[{mod.Name}] Kesz." : $"[{mod.Name}] SIKERTELEN.");
            }

            log("Telepites kesz. A Modulok nezetet erdemes ujranyitni a friss allapothoz.");
        }

        // Ha a friss letoltott modul rts-repo.json-ja "cleanup_dirs"-t deklaral
        // (pl. ["linux","mac"]), azokat a mappakat torli a letoltott modulbol -
        // ezek csak mas platformnak kellenenek, feleslegesen foglalnak helyet.
        // Igy pl. egy win/linux mappaszerkezetu repobol csak a Windows-agat
        // tartjuk meg a telepites utan. Ez FUGGETLEN attol, hogy a modulnak
        // van-e mar rts-menu.json-ja.
        private static void CleanupNonWindowsDirs(string targetPath, string moduleName, Action<string> log)
        {
            try
            {
                string repoConfigPath = Path.Combine(targetPath, "rts-repo.json");
                if (!File.Exists(repoConfigPath)) return;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var config = JsonSerializer.Deserialize<RtsRepoConfig>(File.ReadAllText(repoConfigPath), options);
                if (config?.CleanupDirs == null || config.CleanupDirs.Count == 0) return;

                foreach (var dir in config.CleanupDirs)
                {
                    string fullDir = Path.Combine(targetPath, dir);
                    if (Directory.Exists(fullDir))
                    {
                        Directory.Delete(fullDir, true);
                        log($"[{moduleName}] Nem-Windows mappa torolve: {dir}");
                    }
                }
            }
            catch (Exception ex)
            {
                log($"[{moduleName}] Figyelmeztetes: a felesleges mappak takaritasa nem sikerult - {ex.Message}");
            }
        }

        private static bool IsGitAvailable()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                p!.WaitForExit(3000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        private static bool CloneViaGit(string repo, string targetPath, Action<string> log)
        {
            try
            {
                var psi = new ProcessStartInfo("git", $"clone https://github.com/{repo}.git \"{targetPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                p!.WaitForExit(120000);
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                log($"Git klonozas hiba: {ex.Message}");
                return false;
            }
        }

        private static bool DownloadZip(string repo, string targetPath, string moduleName, Action<string> log)
        {
            string zipUrl = $"https://github.com/{repo}/archive/refs/heads/main.zip";
            string tmpZip = Path.Combine(Path.GetTempPath(), $"rts_{moduleName}.zip");
            string tmpDir = Path.Combine(Path.GetTempPath(), $"rts_{moduleName}");

            try
            {
                using var http = new HttpClient();
                byte[] bytes = http.GetByteArrayAsync(zipUrl).GetAwaiter().GetResult();
                File.WriteAllBytes(tmpZip, bytes);

                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                ZipFile.ExtractToDirectory(tmpZip, tmpDir);

                var extracted = Directory.GetDirectories(tmpDir);
                if (extracted.Length > 0)
                {
                    Directory.Move(extracted[0], targetPath);
                    return true;
                }

                log($"[{moduleName}] Nem talalhato kibontott mappa a ZIP-ben.");
                return false;
            }
            catch (Exception ex)
            {
                log($"[{moduleName}] ZIP letoltes/kibontas hiba: {ex.Message}");
                return false;
            }
            finally
            {
                try { File.Delete(tmpZip); } catch { }
                try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }
            }
        }

        // ================= COMMIT-ALAPU FRISSITES-ELLENORZES =================

        private static string ShortSha(string? sha) =>
            string.IsNullOrEmpty(sha) ? "?" : (sha.Length > 7 ? sha.Substring(0, 7) : sha);

        private static ModuleInstallMeta? ReadInstallMeta(string targetPath)
        {
            try
            {
                string metaPath = Path.Combine(targetPath, InstallMetaFile);
                if (!File.Exists(metaPath)) return null;
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ModuleInstallMeta>(File.ReadAllText(metaPath), options);
            }
            catch { return null; }
        }

        // Uj telepites/frissites utan elmentjuk, PONTOSAN melyik commit-ot
        // toltottuk le - git klonozasnal ezt "git rev-parse HEAD"-del kerdezzuk
        // le, ZIP-es letoltesnel a GitHub API-tol mar amugy is lekert legfrissebb
        // commit-ot hasznaljuk (a ZIP maga nem tartalmaz commit-infot).
        private static void SaveInstallMetaAfterInstall(string targetPath, string repo, string method, Action<string> log)
        {
            try
            {
                string? sha = null;
                DateTime? date = null;

                if (method == "git")
                {
                    sha = GitRevParseHead(targetPath);
                }

                // Ha git-es klonozasnal nem sikerult a sha-t helyben lekerdezni,
                // vagy ZIP-es telepites volt, a GitHub API-tol kerjuk le.
                if (string.IsNullOrEmpty(sha))
                {
                    var (remoteSha, remoteDate, err) = GetLatestRemoteCommit(repo);
                    if (err == null) { sha = remoteSha; date = remoteDate; }
                }

                if (string.IsNullOrEmpty(sha))
                {
                    log($"[{Path.GetFileName(targetPath)}] Figyelmeztetes: nem sikerult megallapitani a letoltott commit-ot - a kesobbi frissites-ellenorzes ennel a modulnal ujra teljes ujratoltest fog javasolni.");
                    return;
                }

                var meta = new ModuleInstallMeta
                {
                    Commit = sha,
                    CommitDate = date,
                    InstalledUtc = DateTime.UtcNow,
                    Method = method
                };
                File.WriteAllText(Path.Combine(targetPath, InstallMetaFile), JsonSerializer.Serialize(meta));
            }
            catch (Exception ex)
            {
                log($"[{Path.GetFileName(targetPath)}] Figyelmeztetes: a telepitesi metaadat mentese sikertelen - {ex.Message}");
            }
        }

        private static string? GitRevParseHead(string repoPath)
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse HEAD")
                {
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p!.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                return p.ExitCode == 0 && output.Length > 0 ? output : null;
            }
            catch { return null; }
        }

        // A GitHub publikus, hitelesites nelkuli API-jat hivja: a repo
        // alapertelmezett agan (HEAD) levo legfrissebb commit sha-jat es
        // datumat adja vissza. Hitelesites nelkul ~60 hivas/ora/IP a limit,
        // ami bőven eleg egy-egy ellenorzeshez vagy ujratelepiteshez.
        private static (string? sha, DateTime? date, string? error) GetLatestRemoteCommit(string repo)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RTS-UpdateCheck", RtsVersion.Version));
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                http.Timeout = TimeSpan.FromSeconds(15);

                string url = $"https://api.github.com/repos/{repo}/commits/HEAD";
                string json = http.GetStringAsync(url).GetAwaiter().GetResult();

                using var doc = JsonDocument.Parse(json);
                string? sha = doc.RootElement.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() : null;
                DateTime? date = null;
                if (doc.RootElement.TryGetProperty("commit", out var commitEl) &&
                    commitEl.TryGetProperty("committer", out var committerEl) &&
                    committerEl.TryGetProperty("date", out var dateEl) &&
                    DateTime.TryParse(dateEl.GetString(), out var parsedDate))
                {
                    date = parsedDate;
                }

                return (sha, date, sha == null ? "ervenytelen API-valasz" : null);
            }
            catch (Exception ex)
            {
                return (null, null, ex.Message);
            }
        }

        // Konnyu-sulyu ellenorzes (nem tolt le/ir semmit) - a Home/betoltokepernyon
        // es az InfoView-n hasznalhato, hogy megmutassa, hany telepitett
        // modulhoz van ujabb valtozat a GitHub-on, anelkul hogy barmit
        // valtoztatna. Csak azokat a modulokat vizsgalja, amik enabled=true
        // ES mar telepitve vannak.
        public static List<ModuleUpdateStatus> CheckForUpdates(string rootPath, List<Models.ModuleInfo> modules)
        {
            var results = new List<ModuleUpdateStatus>();
            string appsDir = Path.Combine(rootPath, "Apps");

            foreach (var mod in modules.Where(m => m.Enabled && m.Visibility != "private"))
            {
                string targetPath = Path.Combine(appsDir, mod.Name);
                bool installed = Directory.Exists(targetPath);
                var status = new ModuleUpdateStatus { Name = mod.Name, Installed = installed };

                if (!installed) { results.Add(status); continue; }

                var localMeta = ReadInstallMeta(targetPath);
                status.LocalCommit = localMeta?.Commit;

                var (remoteSha, remoteDate, err) = GetLatestRemoteCommit(mod.Repo);
                if (err != null) { status.Error = err; results.Add(status); continue; }

                status.RemoteCommit = remoteSha;
                status.RemoteCommitDate = remoteDate;
                status.HasUpdate = localMeta == null || !string.Equals(localMeta.Commit, remoteSha, StringComparison.OrdinalIgnoreCase);
                results.Add(status);
            }

            return results;
        }
    }
}
