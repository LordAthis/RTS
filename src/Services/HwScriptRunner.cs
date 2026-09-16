// Verzio: v1.0.0 - 2026-09-16
// UJ SZOLGALTATAS (round17): a hardver-lekerdezo PowerShell-scriptek
// kicsomagolasa es CSENDES futtatasa.
//
// MIERT VANNAK BEEGETVE (EmbeddedResource) AZ EXE-BE:
// ugyanaz az indok, mint az RTS-info.json / donate.json eseteben (lasd
// RTS.csproj megjegyzeset): ha a scriptek csak kulso fajlkent lennenek
// jelen, egy hianyzo vagy elavult fajl eseten a lekerdezes csendben
// elhasalna. Igy MINDEN build magaval viszi a sajat, hozza tartozo
// script-verziot, es indulaskor kiirja a <InstallRoot>\Scripts\Hw\
// mappaba. A kiirt fajlok TOVABBRA IS megnyithatok es olvashatok a
// felhasznalo szamara (a felhasznalo kifejezett kerese volt, hogy a
// lekerdezesek kulon, lathato .ps1 fajlok legyenek) - csak eppen minden
// inditaskor frissulnek az exe-be egetett, garantaltan egyezo valtozatra.
//
// CSENDES FUTTATAS: -NoProfile -NonInteractive -ExecutionPolicy Bypass,
// CreateNoWindow = true, UseShellExecute = false. NINCS UAC-kérés, NINCS
// villano ablak, NINCS "nyomj meg egy gombot" varakozas.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RTS.Services
{
    public class HwScriptResult
    {
        public bool Ok { get; set; }
        public string StdOut { get; set; } = "";
        public string StdErr { get; set; } = "";
        public int ExitCode { get; set; }
        public string Message { get; set; } = "";
    }

    public static class HwScriptRunner
    {
        // A beegetett scriptek logikai nevei (lasd RTS.csproj).
        private static readonly string[] ScriptFiles =
        {
            "Get-HardwareReport.ps1",
            "Get-SystemInfo.ps1",
            "Get-CpuInfo.ps1",
            "Get-GpuInfo.ps1",
            "Get-DiskInfo.ps1",
            "Get-SensorInfo.ps1",
            "Get-DxDiagInfo.ps1",
            "Ensure-Tool.ps1"
        };

        private const string ResourcePrefix = "RTS.EmbeddedResources.Hw.";

        public static string ScriptsDir => Path.Combine(ModuleRunner.DataDir, "Scripts", "Hw");

        private static bool _extracted;
        private static readonly object ExtractLock = new();

        // Kiirja a beegetett scripteket a lemezre. Idempotens: ha a tartalom
        // mar egyezik, nem ir feleslegesen. Hiba eseten NEM dob kivetelt -
        // a hivo a visszateresi ertekbol tudja, sikerult-e.
        public static bool EnsureScriptsExtracted(Action<string>? log = null)
        {
            lock (ExtractLock)
            {
                if (_extracted) return true;
                try
                {
                    Directory.CreateDirectory(ScriptsDir);
                    var asm = Assembly.GetExecutingAssembly();

                    foreach (string file in ScriptFiles)
                    {
                        string resourceName = ResourcePrefix + file;
                        using Stream? stream = asm.GetManifestResourceStream(resourceName);
                        if (stream == null)
                        {
                            log?.Invoke($"[Eszkozok] Hianyzo beegetett script: {resourceName}");
                            return false;
                        }

                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        string content = reader.ReadToEnd();

                        string target = Path.Combine(ScriptsDir, file);
                        bool needsWrite = true;
                        if (File.Exists(target))
                        {
                            try { needsWrite = File.ReadAllText(target) != content; } catch { needsWrite = true; }
                        }
                        if (needsWrite)
                        {
                            File.WriteAllText(target, content, new UTF8Encoding(false));
                        }
                    }

                    _extracted = true;
                    return true;
                }
                catch (Exception ex)
                {
                    log?.Invoke("[Eszkozok] A lekerdezo scriptek kiirasa sikertelen: " + ex.Message);
                    return false;
                }
            }
        }

        // Egy scriptet futtat csendben, es visszaadja a teljes kimenetet.
        public static async Task<HwScriptResult> RunAsync(
            string scriptFileName,
            string arguments,
            Action<string>? log = null,
            int timeoutSeconds = 300,
            CancellationToken cancellationToken = default)
        {
            var result = new HwScriptResult();

            if (!EnsureScriptsExtracted(log))
            {
                result.Message = "A lekerdezo scripteket nem sikerult elokesziteni.";
                return result;
            }

            string scriptPath = Path.Combine(ScriptsDir, scriptFileName);
            if (!File.Exists(scriptPath))
            {
                result.Message = $"A script nem talalhato: {scriptPath}";
                return result;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = ScriptsDir,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var proc = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                try
                {
                    await proc.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                    result.Message = $"A lekerdezes idotullepes miatt megszakadt ({timeoutSeconds} mp).";
                    result.StdOut = stdout.ToString();
                    result.StdErr = stderr.ToString();
                    return result;
                }

                result.ExitCode = proc.ExitCode;
                result.StdOut = stdout.ToString();
                result.StdErr = stderr.ToString();
                result.Ok = proc.ExitCode == 0;
                result.Message = result.Ok ? "Kesz." : $"A script nem nullas kilepesi kodot adott ({proc.ExitCode}).";

                if (!string.IsNullOrWhiteSpace(result.StdErr))
                {
                    log?.Invoke("[Eszkozok] " + scriptFileName + " hibakimenet: " + result.StdErr.Trim());
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"A script futtatasa sikertelen ({scriptFileName}): {ex.Message}";
                log?.Invoke("[Eszkozok] " + result.Message);
                return result;
            }
        }
    }
}
