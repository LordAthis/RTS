using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Forms;
using RTS.Models;

namespace RTS.Services
{
    // Elso-inditasi telepito: ha az RTS.exe maga mellett nem talal modules.json-t,
    // megkerdezi hova telepitse az adatokat (alapertelmezes: C:\Program Files\RTS),
    // letolti a modules.json-t es minden engedelyezett, publikus modult, majd
    // elmenti a valasztott mappat, hogy legkozelebb ne kelljen ujra kerdezni.
    public static class RtsInstaller
    {
        private const string GithubRawModulesUrl = "https://raw.githubusercontent.com/LordAthis/RTS/main/modules.json";
        private const string ConfigDir = "RTS";
        private const string ConfigFile = "install.json";

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
                    log($"[{mod.Name}] Mar letezik - kihagyva.");
                    continue;
                }

                log($"[{mod.Name}] Telepites: {mod.Repo}");
                bool ok = gitAvailable && CloneViaGit(mod.Repo, targetPath, log);
                if (!ok) ok = DownloadZip(mod.Repo, targetPath, mod.Name, log);
                if (ok) CleanupNonWindowsDirs(targetPath, mod.Name, log);
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
    }
}
