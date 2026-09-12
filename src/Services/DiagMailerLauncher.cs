using System;
using System.Diagnostics;
using System.IO;

namespace RTS.Services
{
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

        public static (bool Ok, string Message) SendLogFolder(Action<string> log)
        {
            try
            {
                string rtsRoot = ModuleRunner.FindRtsRoot();
                string logFolder = Path.Combine(rtsRoot, "LOG");
                Directory.CreateDirectory(logFolder);

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

                log($"[DiagMailer] Kuldes inditasa: {logFolder}");

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
