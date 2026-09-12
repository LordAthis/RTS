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
    // Verzio v0.4.2 - 2026-09-12: MASODIK, EZZEL OSSZEFUGGO HIBA JAVITVA -
    // a felhasznalo altal kuldott valodi logokban ("Access to the path
    // 'pack-....idx' is denied.") kiderult, hogy a sima Directory.Delete-nek
    // MEG AKKOR IS baja lehet, ha csak egy ideiglenes/mar felesleges mappat
    // torlunk: a git altal klonozott ".git" mappa "pack-*.idx" fajljait a
    // git csak-olvashatonak jelzi, es a .NET Directory.Delete(path, true)
    // ilyenkor FELBESZAKAD kozepen - a mar torolt fajlok/almappak NEM
    // allnak vissza. Ez pontosan azt okozta a felhasznalo gepen, hogy a
    // v0.4.1-es "biztonsagos" frissites-ellenorzes soran tobb modulnal a
    // regi mappa reszben kiurult, mielott a torles hibat dobott volna -
    // ettol indult tobb modul egyszerre "Fajl nem talalhato... futtasd a
    // bootstrap.ps1-et" hibaval. Uj ForceDeleteDirectory() segedfuggveny
    // minden torles elott levalasztja a csak-olvashato jelzest minden
    // fajlrol - ez most MINDEN mappatorlesi helyen (ideiglenes letoltesi
    // mappa, regi-peldany takaritas, ZIP-es ideiglenes mappa, nem-Windows
    // almappak) hasznalva van.
    //
    // Verzio v0.4.1 - 2026-09-12: SULYOS HIBA JAVITVA - a v0.4.0-as frissites-
    // ellenorzes ELOSZOR TOROLTE a mar telepitett modul mappajat, es csak
    // UTANA probalta ujratolteni - ha ez sikertelen volt (halozat, GitHub
    // kvota), a modul ures mappaval maradt, es "futtasd a bootstrap.ps1-et"
    // hibaval NEM indult tobbe egyetlen ilyen modul sem. Mostantol a friss
    // peldany egy ideiglenes ".rts-new" mappaba toltodik le, es a regi
    // CSAK a sikeres letoltes UTAN, atomi mozgatassal csereeodik le - a
    // regi peldany minden sikertelen frissitesi kiserletnel erintetlen
    // marad.
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

        // v0.4.1 melle, a felhasznaloi log alapjan talalt masodik hiba javitasa:
        // a sima Directory.Delete(path, true) Windows-on FELBESZAKADHAT, ha
        // menet kozben olyan fajlba fut (pl. egy git-klonozott .git mappa
        // "pack-*.idx" fajljai, amiket a git maga jelol csak-olvashatonak),
        // amit nem tud torolni - es a mar torolt fajlokat/almappakat NEM allitja
        // vissza. Ez okozta pontosan azt, hogy egy "sikertelen" torles a felhasznalo
        // gepen NEM hagyta erintetlenul a regi mappat, hanem felig-meddig
        // kiuritette - onnantol a modul "Fajl nem talalhato... bootstrap.ps1"
        // hibaval nem indult. Ez a segedfuggveny elobb levaltja a csak-olvashato
        // jelzest minden fajlrol, csak utana torol - es maga is elnyeli az
        // esetleges hibat (a hivo dontheti el, mit kezd a sikertelenseggel).
        private static bool ForceDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return true;

                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    try { file.Attributes = FileAttributes.Normal; } catch { }
                }

                Directory.Delete(path, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

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
                bool isUpdate = false;

                if (Directory.Exists(targetPath))
                {
                    // Verzio v0.4.3 - 2026-09-13 JAVITAS: mielott barmilyen
                    // tavoli commit-ellenorzesbe kezdenenk, megnezzuk, hogy a
                    // mar "meglevo" mappa egyaltalan EPP-e - azaz megvan-e a
                    // sajat entry_point fajlja. A felhasznalo gepen talalt
                    // logok szerint tobb modul mappaja korabban (v0.4.0/
                    // v0.4.1 hibak miatt) reszben kiurult, es utana - mivel a
                    // GitHub API kvotaja is ki volt merulve (403 rate limit) -
                    // a v0.4.2-es "biztonsagos" logika soha nem jutott el a
                    // tenyleges ujra-letoltesig, mert a hibas commit-
                    // ellenorzesnel egyszeruen "a meglevo peldany marad"
                    // dontessel leallt - meg akkor is, ha az a "meglevo
                    // peldany" mar bizonyithatoan torott volt. Ha a mappa
                    // igazoltan torott, MOST MAR akkor is nekilatunk az
                    // ujra-letoltesnek, ha a tavoli ellenorzes hibazik -
                    // hiszen a jelenlegi allapotot mindenkepp javitani kell,
                    // nincs mit "megorizni" rajta.
                    bool isBroken = !string.IsNullOrWhiteSpace(mod.EntryPoint) &&
                                     !File.Exists(Path.Combine(targetPath, mod.EntryPoint));

                    // Megnezzuk, van-e ujabb commit a GitHub-on a helyben tarolt
                    // .rts-installed.json alapjan, es ha igen (vagy ha meg
                    // nincs meta - regi, e funkcio elotti telepites),
                    // frissitjuk a modult.
                    //
                    // FONTOS - v0.4.1 JAVITAS: a v0.4.0-as valtozat itt ELOSZOR
                    // TOROLTE a regi, mukodo mappat, es csak UTANA probalta
                    // letolteni az ujat - ha a letoltes barmilyen okbol
                    // (halozati hiba, GitHub API/kvota-korlat, git hiba)
                    // sikertelen volt, a modul ures/hianyos mappaval maradt,
                    // es utana egyetlen modul sem tudott elindulni ("nincs
                    // telepitve, futtasd a bootstrap.ps1-et" uzenettel). Ez
                    // egy tobb modult is tomegesen "eltuntetett" a 10. kor
                    // frissites-ellenorzese soran. MOSTANTOL a regi mappa
                    // MINDIG megmarad addig, amig az uj peldany le nem
                    // toltodott ES ellenorzottan sikeres nem lett - csak
                    // AKKOR csereljuk le. Ha barmi sikertelen, a regi,
                    // mukodo peldany erintetlen marad.
                    var localMeta = ReadInstallMeta(targetPath);
                    var (remoteSha, remoteDate, remoteErr) = GetLatestRemoteCommit(mod.Repo);

                    if (remoteErr != null)
                    {
                        if (isBroken)
                        {
                            log($"[{mod.Name}] A meglevo peldany hianyos ({mod.EntryPoint} nem talalhato) ES a frissites-ellenorzes is sikertelen ({remoteErr}) - a commit-egyeztetes nelkul, kenyszeritve ujratoltjuk.");
                            isUpdate = true;
                        }
                        else
                        {
                            log($"[{mod.Name}] Mar letezik - frissites-ellenorzes sikertelen ({remoteErr}), a meglevo peldany marad.");
                            continue;
                        }
                    }
                    else
                    {
                        bool needsUpdate = isBroken || localMeta == null || !string.Equals(localMeta.Commit, remoteSha, StringComparison.OrdinalIgnoreCase);
                        if (!needsUpdate)
                        {
                            log($"[{mod.Name}] Mar letezik es naprakesz (commit: {ShortSha(remoteSha)}).");
                            continue;
                        }

                        string reason = isBroken ? $"a meglevo peldany hianyos ({mod.EntryPoint} nem talalhato)" : "frissites elerheto";
                        log($"[{mod.Name}] {reason} (helyi: {(localMeta == null ? "ismeretlen (regi telepites)" : ShortSha(localMeta.Commit))} -> uj: {ShortSha(remoteSha)}, {remoteDate:yyyy-MM-dd}) - letoltes ideiglenes helyre...");
                        isUpdate = true;
                    }
                }

                string downloadPath = isUpdate ? targetPath + ".rts-new" : targetPath;
                if (isUpdate && Directory.Exists(downloadPath))
                {
                    ForceDeleteDirectory(downloadPath); // legfeljebb felulirodik, ha nem sikerul teljesen
                }

                log($"[{mod.Name}] Telepites: {mod.Repo}");
                bool ok = gitAvailable && CloneViaGit(mod.Repo, downloadPath, log);
                string method = ok ? "git" : "";
                if (!ok) { ok = DownloadZip(mod.Repo, downloadPath, mod.Name, log); method = ok ? "zip" : ""; }

                if (!ok)
                {
                    // A regi peldany (ha volt) ERINTETLEN maradt - csak a
                    // sikertelen ideiglenes letoltes-kiserletet takaritjuk.
                    ForceDeleteDirectory(downloadPath);
                    log(isUpdate
                        ? $"[{mod.Name}] Frissites SIKERTELEN - a korabbi, mukodo peldany valtozatlanul megmaradt."
                        : $"[{mod.Name}] SIKERTELEN.");
                    continue;
                }

                CleanupNonWindowsDirs(downloadPath, mod.Name, log);
                SaveInstallMetaAfterInstall(downloadPath, mod.Repo, method, log);

                if (isUpdate)
                {
                    // Csak MOST, a sikeres letoltes utan csereljuk le a regit -
                    // a regi mappat egy roviden elo ".rts-old" nevre tesszuk
                    // at (nem toroljuk azonnal), majd az uj kerul a helyere.
                    string oldBackupPath = targetPath + ".rts-old";
                    try
                    {
                        ForceDeleteDirectory(oldBackupPath);
                        Directory.Move(targetPath, oldBackupPath);
                        Directory.Move(downloadPath, targetPath);
                    }
                    catch (Exception ex)
                    {
                        log($"[{mod.Name}] HIBA: a friss peldany sikeresen letoltodott ({downloadPath}), de a helyere-csereles sikertelen ({ex.Message}) - kezzel csereld ki: {targetPath}");
                        continue;
                    }

                    // A regi peldany takaritasat KULON, a csere utan vegezzuk -
                    // ha ez sikertelen (pl. meg mindig zarolt git pack-fajl),
                    // az a friss, mar helyere tett peldanyt NEM veszelyezteti,
                    // csak annyi tortenik, hogy egy ".rts-old" mappa ideiglenesen
                    // ott marad (kesobbi frissitesnel ugyis felulirodik).
                    if (!ForceDeleteDirectory(oldBackupPath))
                    {
                        log($"[{mod.Name}] Megjegyzes: a regi peldany takaritasa ('{oldBackupPath}') nem sikerult teljesen, de ez a mukodest nem befolyasolja.");
                    }
                }

                log($"[{mod.Name}] Kesz.");
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
                        if (ForceDeleteDirectory(fullDir))
                            log($"[{moduleName}] Nem-Windows mappa torolve: {dir}");
                        else
                            log($"[{moduleName}] Figyelmeztetes: a(z) '{dir}' mappa torlese nem sikerult teljesen (nem veszelyes, csak helyet foglal).");
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

                ForceDeleteDirectory(tmpDir);
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
                ForceDeleteDirectory(tmpDir);
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

        // Verzio v0.4.3 - 2026-09-13: EGYSZERU MEMORIA-GYORSITOTAR bevezetve
        // a GitHub-hivasokhoz. A felhasznalo altal kuldott ujabb logokbol
        // kiderult, hogy a "403 (rate limit exceeded)" nem egyszeri
        // balesetet volt, hanem a napi tobbszori "Frissitesek keresese" +
        // "Modulok ujratelepitese" kattintgatas (nemelyik masodperceken
        // belul egymas utan) egy ora alatt tobbszor is kimeritette a
        // GitHub nem-hitelesitett API kvotajat (~60 hivas/ora/IP) - 17
        // modul * tobb egymas utani teljes ellenorzes gyorsan tulmegy
        // ezen. Emiatt a mar korabban serult (l. v0.4.1/v0.4.2) modul-
        // mappak SOHA nem jutottak el a tenyleges ujra-letoltesig, mert a
        // frissites-ellenorzes minden egyes futasnal ujra 403-at kapott,
        // es a "biztonsagos" viselkedes (v0.4.2) ilyenkor egyszeruen
        // kihagyta a modult ("a meglevo peldany marad") - meg akkor is,
        // ha az a "meglevo peldany" mar reg hianyos/torott volt.
        //
        // Ket fuggetlen javitas:
        //  1) Ez a gyorsitotar: ugyanarra a repora 3 percen belul ismet
        //     kert eredmenyt a memoriabol adja vissza, nem hiv ujra API-t -
        //     igy egy "ellenorzes" + kozvetlenul utana egy "ujratelepites"
        //     (vagy ket egymas utani kattintas) osszesen 1x, nem 2x-3x
        //     hasznalja a kvotat repohonkent.
        //  2) Lasd RunFirstTimeSetup: ha egy mar "meglevo" modul-mappabol
        //     hianyzik a sajat entry_point fajlja (tehat biztosan torott,
        //     korabbi hiba miatt), a redownload MOSTANTOL akkor is
        //     megtortenik, ha a tavoli commit-ellenorzes 403-at/hibat ad -
        //     a serult peldanyt nem eszszeru "erintetlenul hagyni".
        private static readonly Dictionary<string, (string? sha, DateTime? date, string? error, DateTime fetchedUtc)> _remoteCommitCache = new();
        private static readonly TimeSpan RemoteCommitCacheTtl = TimeSpan.FromMinutes(3);

        // A GitHub publikus, hitelesites nelkuli API-jat hivja: a repo
        // alapertelmezett agan (HEAD) levo legfrissebb commit sha-jat es
        // datumat adja vissza. Hitelesites nelkul ~60 hivas/ora/IP a limit,
        // ami tobb egymas utani teljes ellenorzesnel/ujratelepitesnel MAR
        // NEM eleg - lasd a fenti v0.4.3 megjegyzest.
        private static (string? sha, DateTime? date, string? error) GetLatestRemoteCommit(string repo)
        {
            if (_remoteCommitCache.TryGetValue(repo, out var cached) &&
                DateTime.UtcNow - cached.fetchedUtc < RemoteCommitCacheTtl)
            {
                return (cached.sha, cached.date, cached.error);
            }

            (string? sha, DateTime? date, string? error) result;
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

                result = (sha, date, sha == null ? "ervenytelen API-valasz" : null);
            }
            catch (Exception ex)
            {
                result = (null, null, ex.Message);
            }

            _remoteCommitCache[repo] = (result.sha, result.date, result.error, DateTime.UtcNow);
            return result;
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
