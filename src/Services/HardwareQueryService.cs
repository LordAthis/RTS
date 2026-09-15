// Verzio: v0.6.0 - 2026-09-14
// UJ SZOLGALTATAS a "2.1 Eszkozok (hardver-lekerdezes)" korhoz. Elvegzi a
// mar korabban egyeztetett (feladatok.md 2.1) lekerdezeseket:
//  - CPU-Z:        -txt=<fajl> (ghost mode, nema, strukturalt riport)
//  - GPU-Z:        minimalizalva, log-fajl mod, par masodperc utan kilo-
//                   jük a folyamatot, a log utolso sorat olvassuk
//  - H.D. Sentinel: /REPORT (rovid UI-felvillanas OK, joviahagyva)
//  - DXDIAG:       /t <fajl> /whql:off
//  - natcv gepadatok: WMI/CIM-bol (gepnev, OS-verzio, helyi fiokok)
// Eredmeny: EGY kozos JSON (<InstallRoot>\data\hardware-info.json).
//
// *** FONTOS, AT KELL NEZNI ELES WINDOWS GEPEN, MIELOTT HASZNALJUK: ***
// Ez a kod ebben a felugyelet nelkuli Cowork-munkametben, Windows/dotnet
// nelkuli sandbox-ban keszult (lasd korabbi korok hasonlo megjegyzeset,
// pl. "Nem tesztelt build"). KULONOSEN a GPU-Z log-fajl-eleresi utja es a
// CPU-Z -txt kimenet PONTOS format-elemzese (parse-olasa) igenyel elo
// tesztet - a lenti parse-oló fuggvenyek szandekosan EGYSZERU, hibatűrő
// "legjobb probalkozas" (best-effort) logikaval keresik a fontosabb
// sorokat, es SOHA nem dobnak hibat kifele, ha valami nem talalhato -
// ilyenkor az adott mezo uresen marad, de a tobbi lekerdezes folytatodik.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RTS.Models;

namespace RTS.Services
{
    public static class HardwareQueryService
    {
        public static string DataFilePath => Path.Combine(ModuleRunner.DataDir, "hardware-info.json");

        // Csak beolvassa a mar korabban elmentett eredmenyt (ha van) - ezt
        // hasznalja a B2 gyors-osszefoglalo ES az A2 reszletes nezet
        // MEGNYITASAKOR, hogy NE induljon automatikusan uj lekerdezes
        // minden alkalommal (lasd specifikacio: "NEM minden induláskor").
        public static HardwareInfo? LoadCached()
        {
            try
            {
                if (!File.Exists(DataFilePath)) return null;
                string json = File.ReadAllText(DataFilePath);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return System.Text.Json.JsonSerializer.Deserialize<HardwareInfo>(json, options);
            }
            catch
            {
                return null;
            }
        }

        private static void Save(HardwareInfo info)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(DataFilePath, System.Text.Json.JsonSerializer.Serialize(info, options));
        }

        // A "Informaciok frissitese" gomb hivja - ez inditja el TENYLEGESEN
        // a kulso eszkozoket. Minden lepes fuggetlen try/catch-ben fut,
        // hogy egy sikertelen eszkoz ne akaszsza meg a tobbit.
        public static async Task<HardwareInfo> RefreshAsync(Action<string>? log = null)
        {
            void Log(string m) => log?.Invoke($"[Eszkozok] {m}");

            var info = LoadCached() ?? new HardwareInfo();
            info.QueriedAtUtc = DateTime.UtcNow;

            info.Machine = QueryMachineInfo(Log);
            info.Cpu = await QueryCpuZAsync(Log);
            info.Gpu = await QueryGpuZAsync(Log);
            info.Disk = await QueryHdSentinelAsync(Log);
            info.DxDiag = await QueryDxDiagAsync(Log);

            Save(info);
            Log("Lekerdezes kesz, eredmeny elmentve: " + DataFilePath);
            return info;
        }

        // ───────────────────────── Gep-alapadatok (WMI/CIM) ─────────────────────────
        private static LocalMachineInfo QueryMachineInfo(Action<string> log)
        {
            var result = new LocalMachineInfo { MachineName = Environment.MachineName };
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    result.OsCaption = os["Caption"]?.ToString()?.Trim() ?? "";
                    result.OsVersion = os["Version"]?.ToString()?.Trim() ?? "";
                }
            }
            catch (Exception ex)
            {
                log("Hiba az OS-adatok lekerdezesekor: " + ex.Message);
            }

            try
            {
                using var userSearcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_UserAccount WHERE LocalAccount = True AND Disabled = False");
                foreach (ManagementObject user in userSearcher.Get())
                {
                    string? name = user["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name)) result.LocalUserAccounts.Add(name);
                }
            }
            catch (Exception ex)
            {
                log("Hiba a felhasznaloi fiokok lekerdezesekor: " + ex.Message);
            }

            return result;
        }

        // ───────────────────────────────── CPU-Z ─────────────────────────────────
        private static async Task<HardwareToolResult> QueryCpuZAsync(Action<string> log)
        {
            var result = new HardwareToolResult { QueriedAtUtc = DateTime.UtcNow };
            try
            {
                await ToolAcquisition.EnsureAsync(ToolId.CpuZ, log);
                string? exe = FindExe(ToolAcquisition.ToolDir(ToolId.CpuZ), "cpuz");
                if (exe == null)
                {
                    result.Error = "CPU-Z nem talalhato (letoltes sikertelen vagy meg nem tortent meg).";
                    result.Available = false;
                    return result;
                }

                string reportFile = Path.Combine(Path.GetTempPath(), "rts_cpuz_report");
                var psi = new ProcessStartInfo(exe, $"-txt=\"{reportFile}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();

                string actualFile = reportFile + ".txt";
                if (File.Exists(actualFile))
                {
                    result.RawText = await File.ReadAllTextAsync(actualFile);
                    result.Summary = ExtractCpuZSummary(result.RawText);
                    result.Available = true;
                    try { File.Delete(actualFile); } catch { }
                }
                else
                {
                    result.Error = "A CPU-Z riport-fajl nem jott letre (-txt kapcsolo - ellenorizendo elo gepen).";
                }
            }
            catch (Exception ex)
            {
                log("CPU-Z lekerdezes hiba: " + ex.Message);
                result.Error = ex.Message;
            }
            return result;
        }

        // "Legjobb probalkozas" kivonatolo - a CPU-Z -txt riport tipikusan
        // "Name" es "Specification" sorokat tartalmaz a [Processor 1]
        // szekcioban. Ha a pontos formatum mashogy alakul elo gepen, ez a
        // fuggveny egyszeruen uresen hagyja a Summary-t (a RawText akkor
        // is elerheto marad a reszletes nezetben).
        private static string ExtractCpuZSummary(string rawText)
        {
            var match = Regex.Match(rawText, @"Name\s*\|?\s*(.+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        // ───────────────────────────────── GPU-Z ─────────────────────────────────
        private static async Task<HardwareToolResult> QueryGpuZAsync(Action<string> log)
        {
            var result = new HardwareToolResult { QueriedAtUtc = DateTime.UtcNow };
            try
            {
                await ToolAcquisition.EnsureAsync(ToolId.GpuZ, log);
                string toolDir = ToolAcquisition.ToolDir(ToolId.GpuZ);
                string? exe = FindExe(toolDir, "gpu-z");
                if (exe == null)
                {
                    result.Error = "GPU-Z nem talalhato (letoltes sikertelen vagy meg nem tortent meg).";
                    result.Available = false;
                    return result;
                }

                // A GPU-Z sajat "GPU-Z.ini"-t olvas a sajat mappajabol, ha
                // ott talal ilyet (portable mod) - ez alapjan iranyithato a
                // sensor-log helye induláskor. A pontos ini-kulcsok EZEN A
                // PONTON MEG NINCSENEK ELO GEPEN LEELLENORIZVE - ha a
                // kovetkezo kulcsok nem valnak be, a GPU-Z sajat,
                // alapertelmezett naplo-helyet kell hasznalni helyettuk.
                string logFile = Path.Combine(Path.GetTempPath(), "rts_gpuz_log.txt");
                string iniPath = Path.Combine(toolDir, "GPU-Z.ini");
                try
                {
                    File.WriteAllText(iniPath,
                        "[General]\r\n" +
                        "OpenAtStartup = 0\r\n" +
                        $"SensorLogFileName = {logFile}\r\n" +
                        "LogSensorsOnStartup = 1\r\n");
                }
                catch { /* nem kritikus - GPU-Z sajat alapertelmezettel is elindul */ }

                var psi = new ProcessStartInfo(exe, "-minimized")
                {
                    UseShellExecute = false,
                    CreateNoWindow = false, // GPU-Z minimalizalva, de nem "no window" - sajat trayikonjahoz
                    WorkingDirectory = toolDir
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    // Par masodpercet adunk neki, hogy elinditson es irjon a log-ba,
                    // majd kilojuk - a specifikacio szerint ("kilőjük a folyamatot").
                    await Task.Delay(TimeSpan.FromSeconds(6));
                    try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                }

                if (File.Exists(logFile))
                {
                    var lines = await File.ReadAllLinesAsync(logFile);
                    string lastLine = lines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? "";
                    result.RawText = lastLine;
                    result.Summary = ExtractGpuZSummary(lastLine);
                    result.Available = !string.IsNullOrWhiteSpace(lastLine);
                    try { File.Delete(logFile); } catch { }
                }
                else
                {
                    result.Error = "A GPU-Z sensor-log fajl nem jott letre - a naplo-eleresi utat elo gepen ellenorizni kell.";
                }
            }
            catch (Exception ex)
            {
                log("GPU-Z lekerdezes hiba: " + ex.Message);
                result.Error = ex.Message;
            }
            return result;
        }

        // A GPU-Z sensor-log elso oszlopa altalaban egy fejlec-sor
        // (oszlopnevek), maga a "GPU nev" nem feltetlen resze a log-nak -
        // ez a mezo emiatt tobbnyire uresen marad, amig elo teszttel nem
        // tisztazodik a GPU-Z altal ENnyleg irt oszlopok sorrendje.
        private static string ExtractGpuZSummary(string lastLogLine) => "";

        // ─────────────────────────────── H.D. Sentinel ───────────────────────────────
        private static async Task<DiskHealthResult> QueryHdSentinelAsync(Action<string> log)
        {
            var result = new DiskHealthResult { QueriedAtUtc = DateTime.UtcNow };
            try
            {
                await ToolAcquisition.EnsureAsync(ToolId.HdSentinelFree, log);
                string? exe = FindExe(ToolAcquisition.ToolDir(ToolId.HdSentinelFree), "hdsentinel");
                if (exe == null)
                {
                    result.Error = "H.D. Sentinel (FREE) nem talalhato (letoltes sikertelen vagy meg nem tortent meg).";
                    result.Available = false;
                    return result;
                }

                string reportFile = Path.Combine(Path.GetTempPath(), "rts_hds_report.html");
                var psi = new ProcessStartInfo(exe, $"/REPORT=\"{reportFile}\"")
                {
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();

                if (File.Exists(reportFile))
                {
                    result.RawText = await File.ReadAllTextAsync(reportFile);
                    result.HealthPercent = ExtractHdsHealthPercent(result.RawText);
                    result.Summary = result.HealthPercent.HasValue
                        ? $"Lemez-egeszseg: {result.HealthPercent}%"
                        : "";
                    result.Available = true;
                    try { File.Delete(reportFile); } catch { }
                }
                else
                {
                    result.Error = "A H.D. Sentinel riport-fajl nem jott letre (/REPORT kapcsolo - ellenorizendo elo gepen).";
                }
            }
            catch (Exception ex)
            {
                log("H.D. Sentinel lekerdezes hiba: " + ex.Message);
                result.Error = ex.Message;
            }
            return result;
        }

        private static int? ExtractHdsHealthPercent(string rawText)
        {
            var match = Regex.Match(rawText, @"Health[^0-9]{0,30}(\d{1,3})\s*%", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int pct)) return pct;
            return null;
        }

        // ───────────────────────────────── DXDIAG ─────────────────────────────────
        private static async Task<HardwareToolResult> QueryDxDiagAsync(Action<string> log)
        {
            var result = new HardwareToolResult { QueriedAtUtc = DateTime.UtcNow };
            try
            {
                string reportFile = Path.Combine(Path.GetTempPath(), "rts_dxdiag_report.txt");
                try { File.Delete(reportFile); } catch { }

                var psi = new ProcessStartInfo("dxdiag.exe", $"/t \"{reportFile}\" /whql:off")
                {
                    UseShellExecute = true
                };
                using var proc = Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();

                // A dxdiag /t aszinkron modon fejezi be az irast - egy
                // rovid, tobbszori ellenorzesi ciklussal varunk ra
                // (max ~15 mp), mielott feladnank.
                for (int i = 0; i < 15 && !File.Exists(reportFile); i++)
                    await Task.Delay(1000);

                if (File.Exists(reportFile))
                {
                    result.RawText = await File.ReadAllTextAsync(reportFile);
                    result.Available = true;
                }
                else
                {
                    result.Error = "A DXDIAG riport-fajl nem jott letre a varakozasi ido alatt.";
                }
            }
            catch (Exception ex)
            {
                log("DXDIAG lekerdezes hiba: " + ex.Message);
                result.Error = ex.Message;
            }
            return result;
        }

        private static string? FindExe(string dir, string nameContains)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;
                return Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f)
                        .Replace(" ", "").Replace("-", "")
                        .Contains(nameContains.Replace("-", ""), StringComparison.OrdinalIgnoreCase))
                    ?? Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
    }
}
