// Verzio: v0.6.1 - 2026-09-15
// UJ SZOLGALTATAS - a csavarkulcs (Eszkozok) panelrol eleri a RustDesk
// telepiteset/inditasat, a SetUpER modulban MAR elokeszitett
// Scripts\Install-RustDesk.ps1-en keresztul (lasd a kiserő jegyzet-
// dokumentumot - a SetUpER-es telepitesi logika NEM duplikalodik itt,
// csak MEGHIVODIK, ugyanugy, ahogy a DiagMailerLauncher is kozvetlenul
// hivja a DiagMailer sajat scriptjet).
//
// Elofeltetel: a SetUpER modul mar telepitve van (Apps\SetUpER). Ha meg
// nincs, egyertelmu uzenetet adunk, es a MOD/STP nezetre iranyitunk -
// NEM probaljuk automatikusan letolteni magat a SetUpER modult innen,
// mert az egy kulon, nagyobb dontes (a felhasznalonak lathatonak kell
// lennie, hogy egy egesz uj modul letoltodik).
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RTS.Services
{
    public static class RustDeskLauncher
    {
        // A SetUpER AppsList.json-jaban rogzitett, tenyleges telepitesi
        // hely (uninstallKey: "RustDesk") - ha ez mashogy valtozna a
        // SetUpER oldalan, itt is frissiteni kell.
        private const string InstalledExePath = @"C:\Program Files\RustDesk\rustdesk.exe";

        public static bool IsInstalled() => File.Exists(InstalledExePath);

        public static bool IsSetUpErReady()
        {
            string setUpErDir = Path.Combine(ModuleRunner.AppsDir, "SetUpER");
            return File.Exists(Path.Combine(setUpErDir, "Scripts", "Install-RustDesk.ps1"))
                && File.Exists(Path.Combine(setUpErDir, "Scripts", "UpDateR.ps1"));
        }

        // Elinditja a mar telepitett RustDesket (pl. hogy a technikus
        // lassa a sajat ID-jat egy tavoli-eleres beallitasahoz).
        public static void LaunchExisting(Action<string> log)
        {
            try
            {
                Process.Start(new ProcessStartInfo(InstalledExePath) { UseShellExecute = true });
                log("[RustDesk] Elinditva.");
            }
            catch (Exception ex)
            {
                log("[RustDesk] Hiba az inditaskor: " + ex.Message);
            }
        }

        // Letoltes+telepites a SetUpER sajat, mar elokeszitett scriptjein
        // keresztul (UpDateR.ps1 -> Install-RustDesk.ps1), UGYANABBAN a
        // sorrendben, ahogy a SetUpER sajat Starter.ps1-je is tenne egy
        // adott appnal - itt csak KOZVETLENUL, a SetUpER interaktiv
        // menuje nelkul hivjuk meg oket, mert egyetlen appra van szukseg.
        public static async Task<(bool Ok, string Message)> EnsureInstalledAsync(Action<string> log)
        {
            if (IsInstalled())
                return (true, "[RustDesk] Mar telepitve van.");

            if (!IsSetUpErReady())
            {
                return (false,
                    "[RustDesk] A SetUpER modul meg nincs telepitve (vagy hianyos) - nyisd meg az STP nezetet " +
                    "a modul telepitesehez, utana probald ujra.");
            }

            string scriptsDir = Path.Combine(ModuleRunner.AppsDir, "SetUpER", "Scripts");
            string updateR = Path.Combine(scriptsDir, "UpDateR.ps1");
            string installer = Path.Combine(scriptsDir, "Install-RustDesk.ps1");

            log("[RustDesk] Letoltes es telepites inditasa a SetUpER-en keresztul...");

            var downloadOk = await RunPowerShellAsync(updateR, "-AppId RustDesk", scriptsDir, log, "RustDesk letoltese");
            if (!downloadOk)
                return (false, "[RustDesk] A letoltes sikertelen volt - reszletek a fenti log-sorokban.");

            var installOk = await RunPowerShellAsync(installer, "", scriptsDir, log, "RustDesk telepitese");
            if (!installOk)
                return (false, "[RustDesk] A telepites sikertelen volt - reszletek a fenti log-sorokban.");

            bool nowInstalled = IsInstalled();
            return nowInstalled
                ? (true, "[RustDesk] Sikeresen telepitve.")
                : (false, "[RustDesk] A telepito lefutott, de a RustDesk nem talalhato a vart helyen - ellenorizd kezzel.");
        }

        private static async Task<bool> RunPowerShellAsync(string scriptPath, string args, string workingDir, Action<string> log, string stepName)
        {
            if (!File.Exists(scriptPath))
            {
                log($"[RustDesk] Hianyzo script ({stepName}): {scriptPath}");
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" {args}")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    log($"[RustDesk] Nem sikerult elinditani: {stepName}");
                    return false;
                }

                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0)
                {
                    log($"[RustDesk] {stepName} hibakoddal fejezodott be ({proc.ExitCode}).");
                    return false;
                }

                log($"[RustDesk] {stepName} kesz.");
                return true;
            }
            catch (Exception ex)
            {
                log($"[RustDesk] Hiba ({stepName}): {ex.Message}");
                return false;
            }
        }
    }
}
