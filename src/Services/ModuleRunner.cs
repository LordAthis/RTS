using System;
using System.Diagnostics;
using System.IO;

namespace RTS.Services
{
    // Egyseges modul-inditó logika: megkeresi az RTS gyokermappajat (ahol a
    // modules.json es az Apps\ mappa van), es a modul entry_point-jat a
    // kiterjesztesnek megfelelo ertelmezovel futtatja.
    // Az RTS.exe mar admin jogosultsaggal fut (lasd app.manifest), igy a
    // gyerek folyamatok orokoltten szinten admin jogot kapnak - kulon
    // "runas" verb-re nincs szukseg.
    public static class ModuleRunner
    {
        private static string? _cachedRoot;

        // Megkeresi az RTS gyokermappajat:
        //  1) a futtathato fajl konyvtarabol felfele lepkedve (max 6 szint),
        //     a modules.json jelenlete alapjan - ez a "repo-bol futtatva" eset;
        //  2) ha az nem talalja, a korabban elmentett telepitesi mappat
        //     (RtsInstaller) - ez a "telepitve, mashonnan futtatva" eset.
        public static string FindRtsRoot()
        {
            if (_cachedRoot != null) return _cachedRoot;

            string? dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir, "modules.json")))
                {
                    _cachedRoot = dir;
                    return dir;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }

            string? saved = RtsInstaller.ReadSavedRoot();
            if (saved != null)
            {
                _cachedRoot = saved;
                return saved;
            }

            // Fallback: nem talalta - a futtatasi konyvtart adja vissza,
            // a hivo fel fogja ismerni a hianyzo modules.json-t.
            _cachedRoot = AppDomain.CurrentDomain.BaseDirectory;
            return _cachedRoot;
        }

        // Az elso-inditasi telepites (RtsInstaller) utan hivando, hogy a
        // kovetkezo FindRtsRoot() mar a friss allapotot lassa, ne a
        // korabban (esetleg meg ures) gyokerre mutasson.
        public static void ResetRootCache() => _cachedRoot = null;

        public static string AppsDir => Path.Combine(FindRtsRoot(), "Apps");

        public static bool ModuleInstalled(string moduleName)
        {
            return Directory.Exists(Path.Combine(AppsDir, moduleName));
        }

        // Egy modul mappajan beluli relativ utvonalat (entry_point vagy
        // brmelyik script) futtatja a kiterjesztesnek megfelelo modon.
        public static (bool Ok, string Message) Run(string moduleName, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return (false, $"[{moduleName}] Nincs megadva belepesi pont a modules.json-ban.");

            string moduleDir = Path.Combine(AppsDir, moduleName);
            string fullPath = Path.Combine(moduleDir, relativePath);

            if (!File.Exists(fullPath))
                return (false, $"[{moduleName}] Fajl nem talalhato: {fullPath} - futtasd eloszor a bootstrap.ps1-et!");

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            ProcessStartInfo psi;

            switch (ext)
            {
                case ".ps1":
                    psi = new ProcessStartInfo("powershell.exe",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{fullPath}\"")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = moduleDir
                    };
                    break;

                case ".bat":
                case ".cmd":
                    psi = new ProcessStartInfo("cmd.exe", $"/c \"\"{fullPath}\"\"")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = moduleDir
                    };
                    break;

                case ".py":
                    psi = new ProcessStartInfo("python.exe", $"\"{fullPath}\"")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = moduleDir
                    };
                    break;

                case ".reg":
                    psi = new ProcessStartInfo("regedit.exe", $"/s \"{fullPath}\"")
                    {
                        UseShellExecute = true,
                        WorkingDirectory = moduleDir
                    };
                    break;

                default:
                    // Ismeretlen tipus - hagyjuk a Windows-ra, hatha van
                    // hozzarendelt alkalmazasa (pl. .exe, .msi).
                    psi = new ProcessStartInfo(fullPath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = moduleDir
                    };
                    break;
            }

            try
            {
                Process.Start(psi);
                return (true, $"[{moduleName}] Elinditva: {Path.GetFileName(fullPath)}");
            }
            catch (Exception ex)
            {
                return (false, $"[{moduleName}] Hiba az inditaskor: {ex.Message}");
            }
        }

        // Kenyelmi alias - ugyanaz mint a Run, de a hivasi hely szandekat
        // vilagosabba teszi ott, ahol egy konkret scriptet inditunk egy mar
        // ismert modulon belul (pl. IWS almodulok).
        public static (bool Ok, string Message) RunScript(string moduleName, string relativeScriptPath)
            => Run(moduleName, relativeScriptPath);
    }
}
