// Verzio: v2.0.0 - 2026-09-16
// ROUND17 ATALAKITAS - EZ A FAJL AZ "INFORMACIOK FRISSITESE" HIBA GYOKERE.
//
// MI VOLT A HIBA (LordAthis 2026-09-15/16-i visszajelzese):
// "A csavarkulcs-ra nyomva a betoltott panelen ott van, hogy Informaciok
//  frissitese. Ez NEM ujratelepites! Hanem elvileg annak a funkcionak a
//  kivaltasa, ami minden hardvert lekerdez. Valahol-valamit osszekevertel,
//  mert most a GPU-z beszerzesen hibara fut, majd betolti a HDSentinel
//  telepitojet, ami mar regen fel van telepitve!"
//
// A pontos ok: a korabbi (v0.6.0) valtozat MINDEN egyes Query*Async
// metodusa ELOSZOR meghivta a ToolAcquisition.EnsureAsync-et, azaz a
// LEKERDEZES es a BESZERZES ossze volt keverve. Minden gombnyomas ujra
// megprobalta letolteni a hianyzo eszkozoket - a GPU-Z 404-re futott (a
// techpowerup Cloudflare-t hasznal, nincs fix URL), majd a HDSentinel
// "hdsentinel_setup.zip"-je kovetkezett, ami NEM portable valtozat, hanem
// a TELEPITO - a kicsomagolt telepitot inditotta el a kod egy
// riport-kapcsoloval, amitol az feltette a telepito-varazslot.
//
// AZ UJ FELALLAS (a felhasznalo altal jovahagyott architektura):
//   BESZERZES  = ToolAcquisition / ToolsBootstrap  (kulon, csak elso
//                indulaskor vagy kezi gombnyomasra)
//   LEKERDEZES = EZ a szolgaltatas: a Scripts\Hw\Get-HardwareReport.ps1
//                orchestratort futtatja csendben, a hatterben. Az
//                orchestrator sorban meghivja a resz-scripteket
//                (Get-SystemInfo / Get-CpuInfo / Get-GpuInfo /
//                Get-DiskInfo), es EGY kozos JSON-t ir.
//
// EZ A SZOLGALTATAS TEHAT SOHA, SEMMILYEN KORULMENYEK KOZOTT NEM TOLT LE
// ES NEM TELEPIT SEMMIT. Ha egy kulso eszkoz (CPU-Z/GPU-Z/HDS) megvan a
// gepen, atadja a scriptnek az eleresi utjat, hogy reszletesebb adatot is
// kapjunk - ha nincs, a lekerdezes a Windows sajat WMI/CIM adataibol
// ugyanugy lefut, csak kevesebb reszlettel.
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RTS.Models;

namespace RTS.Services
{
    // Melyik szekciot frissitse a lekerdezes.
    public enum HwRefreshScope
    {
        All,       // system + cpu + gpu + disk + sensors (a dxdiag NEM - lasd lentebb)
        System,
        Cpu,
        Gpu,
        Disk,
        Sensors,
        DxDiag     // csak kulon keresre - lassu (15-40 mp)
    }

    public static class HardwareQueryService
    {
        public static string DataFilePath => Path.Combine(ModuleRunner.DataDir, "hardware-info.json");

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Egyszerre csak EGY lekerdezes fusson (ha a felhasznalo tobbszor
        // rakattint a gombra, vagy egy hatter-frissites is eppen fut).
        private static readonly SemaphoreSlim RefreshLock = new(1, 1);

        public static bool IsRefreshing { get; private set; }

        // ───────────────────────── Beolvasas (cache) ─────────────────────────
        // Csak a mar korabban elmentett eredmenyt olvassa - NEM indit
        // lekerdezest. Ezt hasznalja a B2 gyors-osszefoglalo es az
        // Eszkozok panel megnyitasa.
        public static HardwareInfo? LoadCached()
        {
            try
            {
                if (!File.Exists(DataFilePath)) return null;
                string json = File.ReadAllText(DataFilePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<HardwareInfo>(json, ReadOptions);
            }
            catch
            {
                // Serult vagy regi formatumu fajl - a kovetkezo lekerdezes
                // ugyis felulirja; addig inkabb "nincs adat", mint hiba.
                return null;
            }
        }

        // Van-e egyaltalan mar elmentett lekerdezes-eredmeny? Ezt nezi az
        // elso indulasi logika (lasd ToolsBootstrap).
        public static bool HasCachedReport()
        {
            try { return File.Exists(DataFilePath) && new FileInfo(DataFilePath).Length > 2; }
            catch { return false; }
        }

        // ───────────────────────── Lekerdezes (frissites) ─────────────────────────
        // Ezt hivja az "Informaciok frissitese" gomb (scope: All) es az
        // egyes eszkozok melletti "Uj gyorsjelentes" gomb (scope: Cpu/Gpu/...).
        public static async Task<HardwareInfo?> RefreshAsync(
            HwRefreshScope scope = HwRefreshScope.All,
            Action<string>? log = null,
            CancellationToken cancellationToken = default)
        {
            void Log(string m) => log?.Invoke("[Eszkozok] " + m);

            await RefreshLock.WaitAsync(cancellationToken);
            IsRefreshing = true;
            try
            {
                string only = ScopeToArgument(scope);
                Log(scope == HwRefreshScope.All
                    ? "Hardver-lekerdezes indul a hatterben (WMI/CIM alapon)..."
                    : $"Reszleges lekerdezes indul: {only}");

                // A MAR MEGLEVO kulso eszkozok utjait atadjuk a scriptnek -
                // de SEMMIT nem szerzunk be. Amelyik nincs meg, annal a
                // script egyszeruen a WMI-adatokra tamaszkodik.
                string toolArgs = BuildToolArguments(scope, Log);

                string outFile = DataFilePath;
                string arguments = $"-OutFile \"{outFile}\" -Only {only}{toolArgs}";

                int timeout = scope == HwRefreshScope.DxDiag ? 180 : 120;
                var run = await HwScriptRunner.RunAsync(
                    "Get-HardwareReport.ps1", arguments, log, timeout, cancellationToken);

                if (!run.Ok && !File.Exists(outFile))
                {
                    Log("A lekerdezes sikertelen: " + run.Message);
                    return LoadCached();
                }

                var info = LoadCached();
                if (info == null)
                {
                    Log("A lekerdezes lefutott, de az eredmeny-fajlt nem sikerult beolvasni: " + outFile);
                    return null;
                }

                // A script sajat hibauzeneteit is kiirjuk a log-panelbe,
                // hogy semmi ne maradjon "csendben elhasalva".
                foreach (string err in info.Errors)
                {
                    if (!string.IsNullOrWhiteSpace(err)) Log("Figyelmeztetes: " + err);
                }

                Log("Lekerdezes kesz, eredmeny elmentve: " + outFile);
                return info;
            }
            catch (Exception ex)
            {
                Log("Hiba a lekerdezes soran: " + ex.Message);
                return LoadCached();
            }
            finally
            {
                IsRefreshing = false;
                RefreshLock.Release();
            }
        }

        // ───────────────────────────── Segedfuggvenyek ─────────────────────────────
        private static string ScopeToArgument(HwRefreshScope scope) => scope switch
        {
            HwRefreshScope.System => "system",
            HwRefreshScope.Cpu    => "cpu",
            HwRefreshScope.Gpu    => "gpu",
            HwRefreshScope.Disk   => "disk",
            HwRefreshScope.Sensors => "sensors",
            HwRefreshScope.DxDiag => "dxdiag",
            _                     => "all"
        };

        // Osszeallitja a MAR MEGLEVO eszkozok eleresi utjait tartalmazo
        // parancssori kapcsolokat. Ez a metodus KIZAROLAG olvas
        // (ToolDetection) - beszerzest SOSEM indit.
        private static string BuildToolArguments(HwRefreshScope scope, Action<string> log)
        {
            string args = "";

            bool wantCpu  = scope == HwRefreshScope.All || scope == HwRefreshScope.Cpu;
            bool wantGpu  = scope == HwRefreshScope.All || scope == HwRefreshScope.Gpu;
            bool wantDisk = scope == HwRefreshScope.All || scope == HwRefreshScope.Disk;
            bool wantSens = scope == HwRefreshScope.All || scope == HwRefreshScope.Sensors;

            if (wantCpu)
            {
                var p = ToolDetection.Detect(ToolId.CpuZ);
                if (p.Installed) args += $" -CpuZExe \"{p.ExecutablePath}\"";
            }
            if (wantGpu)
            {
                var p = ToolDetection.Detect(ToolId.GpuZ);
                if (p.Installed) args += $" -GpuZExe \"{p.ExecutablePath}\"";
            }
            if (wantDisk)
            {
                var p = ToolDetection.Detect(ToolId.HdSentinelFree);
                if (p.Installed) args += $" -HdsExe \"{p.ExecutablePath}\"";
            }
            if (wantSens)
            {
                var p = ToolDetection.Detect(ToolId.LibreHardwareMonitor);
                if (p.Installed) args += $" -LhmExe \"{p.ExecutablePath}\"";
            }

            return args;
        }
    }
}
