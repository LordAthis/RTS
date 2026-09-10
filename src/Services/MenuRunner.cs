using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
