using System;
using System.Diagnostics;
using System.IO;

namespace RTS.Services
{
    // Verzio v0.5.0 - 2026-09-16 (round18: SendHardwareReport)
    // Verzio v0.4.2 - 2026-09-12
    //
    // Uj szolgaltatas: a LOG gomb melletti "boritek" gombra kattintva ezt
    // hivja a MainWindow - a celja, hogy RTS sajat "<gyoker>\LOG" mappajat
    // elkuldje emailben a DiagMailer segitsegevel.
    //
    // MIERT NEM a DiagMailer sajat Launcher.ps1-jet hivjuk?
    // A DiagMailer repoban (LordAthis/DiagMailer) a SendReport.ps1 tud
    // "-LogFolder" parametert fogadni, amivel felul lehet irni, MELYIK
    // mappat kuldje el - ez kellene ide, hiszen RTS sajat "LOG" mappajat
    // akarjuk elkuldeni, nem azt, ami a DiagMailer sajat config.json-jaban
    // (logFolder mezo, alapertelmezetten "..\LOG") esetleg mashogy van
    // beallitva. A Launcher.ps1 (a DiagMailer sajat menuje) VISZONT ezt a
    // parametert nem adja tovabb a SendReport.ps1-nek - csak ConfigPath,
    // ForceCredential, DeleteLogsAfterSend jut el hozza. Emiatt itt
    // KOZVETLENUL a SendReport.ps1-et hivjuk, a sajat LOG-mappankat expliciten
    // atadva - igy nem kell a DiagMailer sajat kodjat modositani ahhoz, hogy
    // ez helyesen mukodjon, es a felhasznalo DiagMailer-konfigja (email,
    // SMTP) is valtozatlan marad barmelyik masik repoban torteno hasznalathoz.
    //
    // Az elevaciot nem kell kulon kezelni: az RTS.exe maga is admin
    // jogosultsaggal fut (lasd app.manifest), a gyerek powershell folyamat
    // ezt orokli.
    public static class DiagMailerLauncher
    {
        private const string DiagMailerRepo = "LordAthis/DiagMailer";

        // ─────────────── ROUND18: hardver-riport elkuldese ───────────────
        // LordAthis 2026-09-16-i kerese: "Plusz gomb (...), az osszegyujtott
        // gepadatok elkuldese... (DiagMailer-el mehet ez is a beallitott
        // cimre!)"
        //
        // Hogyan: a DiagMailer SendReport.ps1-je EGY MAPPAT kuld el, ezert
        // osszeallitunk neki egy sajat, ideiglenes mappat a LOG alatt, amibe
        // bekerul (a) a nyers hardware-info.json, es (b) egy ember altal is
        // olvashato osszefoglalo szoveg. Igy a DiagMailer kodjat NEM kell
        // modositani, a felhasznalo email/SMTP beallitasa valtozatlan marad.
        public static (bool Ok, string Message) SendHardwareReport(Action<string> log)
        {
            try
            {
                string jsonPath = HardwareQueryService.DataFilePath;
                if (!File.Exists(jsonPath))
                {
                    return (false,
                        "[DiagMailer] Meg nincs hardver-lekerdezes eredmeny - kattints eloszor az " +
                        "\"Informaciok frissitese\" gombra, utana kuldheto el.");
                }

                string rtsRoot = ModuleRunner.FindRtsRoot();
                string outDir = Path.Combine(rtsRoot, "LOG",
                    "hardver-riport-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(outDir);

                File.Copy(jsonPath, Path.Combine(outDir, "hardware-info.json"), overwrite: true);

                // Ember altal olvashato valtozat is - hogy a cimzettnek ne
                // kelljen JSON-t bongesznie.
                try
                {
                    var info = HardwareQueryService.LoadCached();
                    if (info != null)
                    {
                        File.WriteAllText(
                            Path.Combine(outDir, "hardver-osszefoglalo.txt"),
                            BuildReadableSummary(info),
                            System.Text.Encoding.UTF8);
                    }
                }
                catch { /* a JSON attol meg megy */ }

                return SendFolder(outDir, log, "[DiagMailer] Hardver-riport kuldese inditva");
            }
            catch (Exception ex)
            {
                return (false, "[DiagMailer] Hiba a hardver-riport osszeallitasakor: " + ex.Message);
            }
        }

        private static string BuildReadableSummary(RTS.Models.HardwareInfo info)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RTS - hardver-riport");
            sb.AppendLine("Keszult: " + (info.QueriedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "(ismeretlen)"));
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();

            void Section(string title, RTS.Models.HwSectionBase? sec)
            {
                if (sec == null) return;
                sb.AppendLine("### " + title);
                if (!string.IsNullOrWhiteSpace(sec.RawSource)) sb.AppendLine("Adatforras: " + sec.RawSource);
                sb.AppendLine();
                sb.AppendLine(sec.RawText);
                if (sec.Errors is { Count: > 0 })
                {
                    sb.AppendLine();
                    sb.AppendLine("Figyelmeztetesek:");
                    foreach (string e in sec.Errors) sb.AppendLine("  - " + e);
                }
                sb.AppendLine();
                sb.AppendLine(new string('-', 60));
                sb.AppendLine();
            }

            Section("Rendszer, alaplap, memoria", info.System);
            Section("Processzor", info.Cpu);
            Section("Videokartya", info.Gpu);
            Section("Lemezek es SMART", info.Disk);
            Section("Homerseklet es szenzorok", info.Sensors);
            Section("DXDIAG", info.DxDiag);

            return sb.ToString();
        }

        public static (bool Ok, string Message) SendLogFolder(Action<string> log)
        {
            string rtsRoot2 = ModuleRunner.FindRtsRoot();
            string logFolder2 = Path.Combine(rtsRoot2, "LOG");
            Directory.CreateDirectory(logFolder2);
            return SendFolder(logFolder2, log, "[DiagMailer] Kuldes inditasa");
        }

        // A ket kuldes (LOG mappa / hardver-riport) kozos resze.
        private static (bool Ok, string Message) SendFolder(string folderToSend, Action<string> log, string startMessage)
        {
            try
            {
                string logFolder = folderToSend;

                string diagMailerDir = Path.Combine(ModuleRunner.AppsDir, "DiagMailer");
                string sendReportScript = Path.Combine(diagMailerDir, "SendReport.ps1");
                string configPath = Path.Combine(diagMailerDir, "config.json");
                string configExample = Path.Combine(diagMailerDir, "config.json.example");

                if (!Directory.Exists(diagMailerDir) || !File.Exists(sendReportScript))
                {
                    return (false,
                        "[DiagMailer] Nincs telepitve (vart hely: " + diagMailerDir + "). " +
                        "Telepitsd a MOD nezetben, vagy futtasd a 'Modulok ujratelepitese' gombot, majd probald ujra.");
                }

                if (!File.Exists(configPath))
                {
                    if (File.Exists(configExample))
                    {
                        File.Copy(configExample, configPath);
                        log("[DiagMailer] config.json letrehozva a peldafajlbol - toltsd ki (email, SMTP), majd probald ujra a kuldest.");
                        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{configPath}\"") { UseShellExecute = true });
                    }
                    else
                    {
                        log("[DiagMailer] Hianyzik mind a config.json, mind a config.json.example - a telepites hianyos.");
                    }
                    return (false, "[DiagMailer] Kuldes megszakitva - eloszor allitsd be a config.json-t.");
                }

                log($"{startMessage}: {logFolder}");

                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{sendReportScript}\" -ConfigPath \"{configPath}\" -LogFolder \"{logFolder}\"")
                {
                    UseShellExecute = true,
                    WorkingDirectory = diagMailerDir
                };
                Process.Start(psi);

                return (true, "[DiagMailer] Elinditva - a SendReport.ps1 sajat ablakaban kovetheto a kuldes allapota.");
            }
            catch (Exception ex)
            {
                return (false, "[DiagMailer] Hiba a kuldes inditasakor: " + ex.Message);
            }
        }
    }
}
