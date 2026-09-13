using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RTS.Models;

namespace RTS.Services
{
    // Egy rts-menu.json-beli MenuItem tenyleges vegrehajtasa.
    //  - "script" tipus: a kimenet BEFOGVA, soronkent a log callbacken
    //    keresztul folyik az RTS sajat log-paneljebe - nincs kulon ablak.
    //  - "shell" tipus: kozvetlen parancs/CLSID/shell: URI - ez tipikusan
    //    sajat Windows-dialogust/ablakot nyit (pl. Vezerlopult), ezt nem
    //    lehet es nem is erdemes befogni.
    //  - "reg" tipus: .reg fajl csendes alkalmazasa.
    //  - RequiresConsole=true eseten (ritka, tenylegesen interaktiv bemenetet
    //    varo script) kulon, lathato konzolablakban indul (ModuleRunner-en
    //    keresztul), ez a "vegso eset", ahogy kertek.
    public static class MenuRunner
    {
        // Verzio v0.5.0 - 2026-09-13: uj, AWAITABLE valtozat a "Kedvenc
        // feladatok" (Favorites) sorozat-futtatashoz - ott meg kell varni,
        // amig egy tetel lefut, mielott a kovetkezo elindulna. A "script"/
        // "reg" tipusnal ez pontos (megvarjuk a folyamat kilepeset), a
        // "shell" tipusnal (sajat ablakot/dialogust nyito parancsok) ezt
        // nem lehet megbizhatoan megvarni - ott elinditjuk es azonnal
        // tovabblepunk, naplozva, hogy ez kezi/felugyeleti figyelmet
        // igenyelhet.
        public static async Task ExecuteAsync(string moduleName, MenuItem item, string selectedOs, Action<string> log)
        {
            string moduleDir = Path.Combine(ModuleRunner.AppsDir, moduleName);

            string effectiveType = item.Type;
            string effectivePath = item.Path;
            if (item.OsOverrides != null && item.OsOverrides.TryGetValue(selectedOs, out var ov))
            {
                effectiveType = ov.Type;
                effectivePath = ov.Path;
            }

            string tag = $"[{moduleName}/{item.Name}]";

            if (item.RequiresConsole || effectiveType == "shell")
            {
                log($"{tag} Kulon ablakban/parancskent inditva - ez a sorozatban NEM varhato meg automatikusan, kezi ellenorzest igenyelhet.");
                Execute(moduleName, item, selectedOs, log);
                return;
            }

            if (effectiveType == "reg")
            {
                RunReg(tag, moduleDir, effectivePath, log);
                return;
            }

            await RunScriptCapturedAsync(tag, moduleName, moduleDir, effectivePath, log);
        }

        private static Task RunScriptCapturedAsync(string tag, string moduleName, string moduleDir, string relativePath, Action<string> log)
        {
            string fullPath = Path.Combine(moduleDir, relativePath);
            if (!File.Exists(fullPath))
            {
                log($"{tag} Fajl nem talalhato: {fullPath} - futtasd eloszor a bootstrap-ot / modul-telepitest!");
                return Task.CompletedTask;
            }

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            if (ext != ".ps1" && ext != ".bat" && ext != ".cmd")
            {
                log($"{tag} Ismeretlen script-tipus ({ext}) a sorozatban - kulon ablakban probaljuk, nem varhato meg.");
                var result = ModuleRunner.Run(moduleName, relativePath);
                log($"{tag} {result.Message}");
                return Task.CompletedTask;
            }

            string fileName = (ext == ".ps1") ? "powershell.exe" : "cmd.exe";
            var argList = new System.Collections.Generic.List<string>();
            if (ext == ".ps1")
            {
                argList.Add("-NoProfile"); argList.Add("-ExecutionPolicy"); argList.Add("Bypass");
                argList.Add("-File"); argList.Add(fullPath);
            }
            else
            {
                argList.Add("/c"); argList.Add(fullPath);
            }

            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = moduleDir
            };
            foreach (var a in argList) psi.ArgumentList.Add(a);

            log($"{tag} Inditas (sorozatban, kimenet befogva)...");

            var tcs = new TaskCompletionSource<bool>();
            try
            {
                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log($"{tag} {e.Data}"); };
                process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log($"{tag} [HIBA] {e.Data}"); };
                process.Exited += (s, e) =>
                {
                    log($"{tag} Lefutott (kilepokod: {process.ExitCode}).");
                    process.Dispose();
                    tcs.TrySetResult(true);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                log($"{tag} Hiba az inditaskor: {ex.Message}");
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        }

        public static void Execute(string moduleName, MenuItem item, string selectedOs, Action<string> log)
        {
            string moduleDir = Path.Combine(ModuleRunner.AppsDir, moduleName);

            // OS-fuggo felulbiralas alkalmazasa, ha van
            string effectiveType = item.Type;
            string effectivePath = item.Path;
            if (item.OsOverrides != null && item.OsOverrides.TryGetValue(selectedOs, out var ov))
            {
                effectiveType = ov.Type;
                effectivePath = ov.Path;
            }

            string tag = $"[{moduleName}/{item.Name}]";

            if (item.RequiresConsole && effectiveType == "script")
            {
                log($"{tag} Kulon ablakban inditva (interaktiv script)...");
                var result = ModuleRunner.RunScript(moduleName, effectivePath);
                log($"{tag} {result.Message}");
                return;
            }

            switch (effectiveType)
            {
                case "shell":
                    RunShell(tag, item, moduleDir, log);
                    break;

                case "reg":
                    RunReg(tag, moduleDir, effectivePath, log);
                    break;

                case "script":
                default:
                    RunScriptCaptured(tag, moduleName, moduleDir, effectivePath, log);
                    break;
            }
        }

        private static void RunShell(string tag, MenuItem item, string moduleDir, Action<string> log)
        {
            try
            {
                Process.Start(new ProcessStartInfo(item.Command)
                {
                    UseShellExecute = true,
                    WorkingDirectory = moduleDir
                });
                log($"{tag} Megnyitva: {item.Command}");
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(item.FallbackCommand))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(item.FallbackCommand)
                        {
                            UseShellExecute = true,
                            WorkingDirectory = moduleDir
                        });
                        log($"{tag} Elsodleges parancs sikertelen ({ex.Message}), tartalek mukodott: {item.FallbackCommand}");
                        return;
                    }
                    catch (Exception ex2)
                    {
                        log($"{tag} Hiba (elsodleges es tartalek parancs is sikertelen): {ex2.Message}");
                        return;
                    }
                }
                log($"{tag} Hiba a megnyitaskor: {ex.Message}");
            }
        }

        private static void RunReg(string tag, string moduleDir, string relativePath, Action<string> log)
        {
            string fullPath = Path.Combine(moduleDir, relativePath);
            if (!File.Exists(fullPath))
            {
                log($"{tag} Fajl nem talalhato: {fullPath}");
                return;
            }
            try
            {
                var psi = new ProcessStartInfo("regedit.exe")
                {
                    UseShellExecute = true,
                    WorkingDirectory = moduleDir
                };
                psi.ArgumentList.Add("/s");
                psi.ArgumentList.Add(fullPath);
                Process.Start(psi);
                log($"{tag} Registry-beallitas alkalmazva: {Path.GetFileName(fullPath)}");
            }
            catch (Exception ex)
            {
                log($"{tag} Hiba a registry-beallitas alkalmazasakor: {ex.Message}");
            }
        }

        private static void RunScriptCaptured(string tag, string moduleName, string moduleDir, string relativePath, Action<string> log)
        {
            string fullPath = Path.Combine(moduleDir, relativePath);
            if (!File.Exists(fullPath))
            {
                log($"{tag} Fajl nem talalhato: {fullPath} - futtasd eloszor a bootstrap-ot / modul-telepitest!");
                return;
            }

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            string fileName;
            var argList = new System.Collections.Generic.List<string>();

            if (ext == ".ps1")
            {
                fileName = "powershell.exe";
                argList.Add("-NoProfile");
                argList.Add("-ExecutionPolicy");
                argList.Add("Bypass");
                argList.Add("-File");
                argList.Add(fullPath);
            }
            else if (ext == ".bat" || ext == ".cmd")
            {
                fileName = "cmd.exe";
                argList.Add("/c");
                argList.Add(fullPath);
            }
            else
            {
                log($"{tag} Ismeretlen script-tipus ({ext}) - kulon ablakban probaljuk inditani.");
                var result = ModuleRunner.Run(moduleName, relativePath);
                log($"{tag} {result.Message}");
                return;
            }

            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = moduleDir
            };
            foreach (var a in argList) psi.ArgumentList.Add(a);

            log($"{tag} Inditas (RTS-en belul, kimenet befogva)...");

            try
            {
                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) log($"{tag} {e.Data}");
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) log($"{tag} [HIBA] {e.Data}");
                };
                process.Exited += (s, e) =>
                {
                    log($"{tag} Lefutott (kilepokod: {process.ExitCode}).");
                    process.Dispose();
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                log($"{tag} Hiba az inditaskor: {ex.Message}");
            }
        }
    }
}
