// Verzio: v2.0.0 - 2026-09-16
// ROUND17 ATALAKITAS - a kulso segedeszkozok BESZERZESE (telepites /
// frissites). Ez a fajl MOSTANTOL KIZAROLAG a beszerzesert felel - a
// hardver-lekerdezes teljesen kulon utat jar (HardwareQueryService +
// Scripts\Hw\*.ps1). A ketto osszekeverese volt a round16 fo hibaja.
//
// HAROM DOLOG VALTOZOTT MEG, MINDHARMO ELO HIBA MIATT:
//
// 1. "AZ UJ VERZIO ALLANDOAN MEGNYITJA A TECHPOWER OLDALAT" (LordAthis,
//    2026-09-16). A round16-ban a sikertelen GPU-Z beszerzes automatikusan
//    megnyitotta a letoltesi oldalt a bongeszoben - es mivel a beszerzes
//    MINDEN indulaskor ES minden frissiteskor ujra lefutott, a bongeszo
//    ujra meg ujra felugrott. MOSTANTOL: a kod SEMMIKOR nem nyit meg
//    magatol bongeszot. A letoltesi oldal linkjet visszaadjuk az
//    uzenetben, es KIZAROLAG a felhasznalo sajat, kifejezett kattintasara
//    nyilik meg (lasd ToolsView "Letoltesi oldal" gomb).
//
// 2. GPU-Z: a "us1-dl.techpowerup.com/files/GPU-Z.{verzio}.exe" minta
//    404-et adott. Elo ellenorzessel (2026-09-16) kiderult, hogy a
//    techpowerup Cloudflare mogott, KETLEPCSOS POST-tal (eloszor egy
//    verzio-azonosito, majd egy szerver-azonosito) szolgalja ki a
//    letoltest, es a verzio-azonosito kiadasonkent valtozik - vagyis FIX,
//    verzioval parameterezheto URL NEM LETEZIK. Ezert a beszerzes a
//    Windows sajat csomagkezelojere (winget) valt, ahogy azt LordAthis
//    is javasolta (GPUZ-Silent.md).
//
// 3. H.D. SENTINEL: a korabbi "hdsentinel_setup.zip" NEM portable
//    valtozat, hanem a TELEPITO. A kod kicsomagolta, majd a kicsomagolt
//    TELEPITOT inditotta el egy riport-kapcsoloval - ettol jott fel a
//    telepito-varazslo egy MAR TELEPITETT programhoz. Mostantol a HDS-t a
//    SetUpER sajat, mar meglevo telepito-csovezeteke kezeli (AppsList.json,
//    id: "HDS"), es a jelenletet a ToolDetection ismeri fel a registrybol.
using System;
using System.Text.Json;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RTS.Services
{
    public enum ToolId
    {
        CpuZ,
        GpuZ,
        HdSentinelFree,
        ResourceHacker,

        // UJ, round17 (LordAthis 2026-09-16-i kerese: "Plusz ket program
        // beilleszteni a Csavarkulcs panelre").
        LibreHardwareMonitor,
        HwMonitor
    }

    public class ToolAcquisitionResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public string? ExecutablePath { get; set; }

        // Ha az automatikus beszerzes nem lehetseges, ide kerul a hivatalos
        // letoltesi oldal cime - a felulet ebbol tud egy KATTINTHATO gombot
        // ajanlani. A kod magatol SOSEM nyitja meg.
        public string? ManualDownloadUrl { get; set; }
    }

    // Egy eszkoz frissitesi allapota (a "Frissites" gombhoz).
    public class ToolUpdateStatus
    {
        public bool Installed { get; set; }
        public bool UpdateAvailable { get; set; }
        public string InstalledVersion { get; set; } = "";
        public string AvailableVersion { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public static class ToolAcquisition
    {
        // A sajat, hordozhato peldanyok helye. A ToolDetection ELOSZOR ide
        // nez, de MAR NEM CSAK ide (lasd ott a registry/Program Files
        // agakat is).
        public static string ToolsDir => Path.Combine(ModuleRunner.AppsDir, "Tools");

        public static string ToolDir(ToolId tool) => Path.Combine(ToolsDir, tool.ToString());

        // Hivatalos letoltesi oldalak - CSAK a felhasznaloi kattintasra
        // megnyilo "Letoltesi oldal" gombhoz, automatikus megnyitas NINCS.
        public static string DownloadPageUrl(ToolId tool) => tool switch
        {
            ToolId.CpuZ                 => "https://www.cpuid.com/softwares/cpu-z.html",
            ToolId.GpuZ                 => "https://www.techpowerup.com/download/techpowerup-gpu-z/",
            ToolId.HdSentinelFree       => "https://www.hdsentinel.com/download.php",
            ToolId.ResourceHacker       => "http://www.angusj.com/resourcehacker/",
            ToolId.LibreHardwareMonitor => "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/latest",
            ToolId.HwMonitor            => "https://www.cpuid.com/softwares/hwmonitor.html",
            _                           => ""
        };

        // Egyszerre csak EGY beszerzes fusson (round16-os versenyhelyzet-
        // javitas - tovabbra is ervenyes es szukseges).
        private static readonly SemaphoreSlim AcquireLock = new(1, 1);

        // ─────────────────────────── Jelenlet-ellenorzes ───────────────────────────
        // FONTOS: ez MOSTANTOL a TELJES gepet nezi (sajat mappa + registry +
        // Program Files), nem csak az Apps\Tools\ mappat. Pontosan ez volt a
        // "mar telepitve van, megis ujra akarja telepiteni" hiba oka.
        public static bool IsPresent(ToolId tool) => ToolDetection.Detect(tool).Installed;

        public static ToolPresence Presence(ToolId tool) => ToolDetection.Detect(tool);

        // ─────────────────────────── Telepites ───────────────────────────
        // KIZAROLAG akkor fut le, ha a hivo KIFEJEZETTEN keri (elso
        // indulaskori beallitas, vagy a felhasznalo "Telepites" gombja).
        // A hardver-lekerdezes SOSEM hivja meg.
        public static async Task<ToolAcquisitionResult> EnsureAsync(ToolId tool, Action<string>? log = null)
        {
            void Log(string m) => log?.Invoke($"[Eszkozok] {m}");
            var desc = ToolDetection.Describe(tool);

            var already = ToolDetection.Detect(tool);
            if (already.Installed)
            {
                return new ToolAcquisitionResult
                {
                    Ok = true,
                    Message = $"{desc.DisplayName} mar rendelkezesre all ({already.Source}).",
                    ExecutablePath = already.ExecutablePath
                };
            }

            await AcquireLock.WaitAsync();
            try
            {
                // Ujra-ellenorzes a zar megszerzese UTAN (round16-os
                // versenyhelyzet-javitas).
                var recheck = ToolDetection.Detect(tool);
                if (recheck.Installed)
                {
                    return new ToolAcquisitionResult
                    {
                        Ok = true,
                        Message = $"{desc.DisplayName} mar rendelkezesre all ({recheck.Source}).",
                        ExecutablePath = recheck.ExecutablePath
                    };
                }

                // A H.D. Sentinel a SetUpER sajat telepito-csovezeteken megy
                // (ott mar letezik "HDS" azonositoval) - NEM toltunk le
                // semmilyen zip-et hozza.
                if (tool == ToolId.HdSentinelFree)
                {
                    return new ToolAcquisitionResult
                    {
                        Ok = false,
                        Message = "A Hard Disk Sentinel telepiteset a SetUpER modul vegzi - nyisd meg az STP nezetet, " +
                                  "es onnan inditsd a telepitest (a korabbi, kozvetlen zip-letoltes hibas volt: az a " +
                                  "csomag nem hordozhato valtozat, hanem a telepito).",
                        ManualDownloadUrl = DownloadPageUrl(tool)
                    };
                }

                if (desc.WingetIds.Length == 0)
                {
                    return new ToolAcquisitionResult
                    {
                        Ok = false,
                        Message = $"{desc.DisplayName}: nincs beallitva automatikus telepitesi mod.",
                        ManualDownloadUrl = DownloadPageUrl(tool)
                    };
                }

                Log($"{desc.DisplayName} telepitese a Windows csomagkezelojevel (winget: {desc.WingetId})...");
                var wr = await RunEnsureToolAsync(desc.WingetIds, "install", log);

                if (wr == null)
                {
                    return new ToolAcquisitionResult
                    {
                        Ok = false,
                        Message = $"{desc.DisplayName}: a telepito script futtatasa sikertelen.",
                        ManualDownloadUrl = DownloadPageUrl(tool)
                    };
                }

                if (!wr.WingetAvailable)
                {
                    return new ToolAcquisitionResult
                    {
                        Ok = false,
                        Message = $"{desc.DisplayName}: {wr.Message}",
                        ManualDownloadUrl = DownloadPageUrl(tool)
                    };
                }

                var after = ToolDetection.Detect(tool);
                if (wr.Installed || after.Installed)
                {
                    Log($"{desc.DisplayName}: {wr.Message}");
                    return new ToolAcquisitionResult
                    {
                        Ok = true,
                        Message = $"{desc.DisplayName} kesz. {wr.Message}",
                        ExecutablePath = after.ExecutablePath
                    };
                }

                Log($"{desc.DisplayName}: {wr.Message}");
                return new ToolAcquisitionResult
                {
                    Ok = false,
                    Message = $"{desc.DisplayName}: {wr.Message}",
                    ManualDownloadUrl = DownloadPageUrl(tool)
                };
            }
            catch (Exception ex)
            {
                Log($"Hiba a(z) {desc.DisplayName} beszerzesekor: {ex.Message}");
                return new ToolAcquisitionResult
                {
                    Ok = false,
                    Message = $"Hiba a(z) {desc.DisplayName} beszerzesekor: {ex.Message}",
                    ManualDownloadUrl = DownloadPageUrl(tool)
                };
            }
            finally
            {
                AcquireLock.Release();
            }
        }

        // ─────────────────────── Frissites-ellenorzes / frissites ───────────────────────
        // A felhasznalo kerese (2026-09-16): "ha telepitve van, akkor ugyan
        // tovabb lep, de egy masik ellenorzo rutint is indit, hogy van-e
        // frissebb verzio! Ebben az esetben a megnyitas mellett a frissites
        // jelenjen meg pluszban!"
        //
        // FONTOS: ez a metodus SEMMIT nem telepit - csak megnezi az
        // allapotot. A tenyleges frissitest az UpdateAsync vegzi.
        public static async Task<ToolUpdateStatus> CheckUpdateAsync(ToolId tool, Action<string>? log = null)
        {
            var desc = ToolDetection.Describe(tool);
            var status = new ToolUpdateStatus();

            var presence = ToolDetection.Detect(tool);
            status.Installed = presence.Installed;
            status.InstalledVersion = presence.Version;

            if (!presence.Installed || desc.WingetIds.Length == 0)
            {
                status.Message = presence.Installed
                    ? "Frissites-ellenorzes ehhez az eszkozhoz nem erheto el."
                    : "Nincs telepitve.";
                return status;
            }

            var wr = await RunEnsureToolAsync(desc.WingetIds, "check", log);
            if (wr == null || !wr.WingetAvailable)
            {
                status.Message = wr?.Message ?? "A frissites-ellenorzes nem futott le.";
                return status;
            }

            status.UpdateAvailable = wr.UpdateAvailable;
            status.AvailableVersion = wr.AvailableVersion;
            if (!string.IsNullOrWhiteSpace(wr.InstalledVersion)) status.InstalledVersion = wr.InstalledVersion;
            status.Message = wr.Message;
            return status;
        }

        public static async Task<ToolAcquisitionResult> UpdateAsync(ToolId tool, Action<string>? log = null)
        {
            void Log(string m) => log?.Invoke($"[Eszkozok] {m}");
            var desc = ToolDetection.Describe(tool);

            if (desc.WingetIds.Length == 0)
            {
                return new ToolAcquisitionResult
                {
                    Ok = false,
                    Message = $"{desc.DisplayName}: automatikus frissites ehhez az eszkozhoz nem erheto el.",
                    ManualDownloadUrl = DownloadPageUrl(tool)
                };
            }

            await AcquireLock.WaitAsync();
            try
            {
                Log($"{desc.DisplayName} frissitese (winget: {desc.WingetId})...");
                var wr = await RunEnsureToolAsync(desc.WingetIds, "upgrade", log);

                if (wr == null || !wr.WingetAvailable)
                {
                    return new ToolAcquisitionResult
                    {
                        Ok = false,
                        Message = $"{desc.DisplayName}: {(wr?.Message ?? "a frissites nem futott le.")}",
                        ManualDownloadUrl = DownloadPageUrl(tool)
                    };
                }

                Log($"{desc.DisplayName}: {wr.Message}");
                var after = ToolDetection.Detect(tool);
                return new ToolAcquisitionResult
                {
                    Ok = wr.Changed || !wr.UpdateAvailable,
                    Message = $"{desc.DisplayName}: {wr.Message}",
                    ExecutablePath = after.ExecutablePath
                };
            }
            finally
            {
                AcquireLock.Release();
            }
        }

        // ─────────────────────────── Megnyitas ───────────────────────────
        // A mar telepitett eszkoz elinditasa (a "Megnyitas" gomb).
        public static bool LaunchExisting(ToolId tool, Action<string>? log = null)
        {
            var desc = ToolDetection.Describe(tool);
            var presence = ToolDetection.Detect(tool);
            if (!presence.Installed || presence.ExecutablePath == null)
            {
                log?.Invoke($"[Eszkozok] {desc.DisplayName} nem talalhato a gepen - eloszor telepitsd.");
                return false;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = presence.ExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(presence.ExecutablePath) ?? "",
                    UseShellExecute = true
                });
                log?.Invoke($"[Eszkozok] {desc.DisplayName} elinditva.");
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Eszkozok] {desc.DisplayName} inditasa sikertelen: {ex.Message}");
                return false;
            }
        }

        // A hivatalos letoltesi oldal megnyitasa - KIZAROLAG a felhasznalo
        // sajat kattintasara hivhato (lasd a fajl fejleceben az 1. pontot).
        public static void OpenDownloadPage(ToolId tool, Action<string>? log = null)
        {
            string url = DownloadPageUrl(tool);
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                log?.Invoke($"[Eszkozok] Letoltesi oldal megnyitva: {url}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Eszkozok] A letoltesi oldal megnyitasa sikertelen: {ex.Message}");
            }
        }

        // ─────────────────────── winget-script futtatasa ───────────────────────
        private class EnsureToolJson
        {
            public bool WingetAvailable { get; set; }
            public bool Installed { get; set; }
            public string InstalledVersion { get; set; } = "";
            public string AvailableVersion { get; set; } = "";
            public bool UpdateAvailable { get; set; }
            public bool Changed { get; set; }
            public string Message { get; set; } = "";
        }

        // Tobb jelolt winget-azonositot adunk at: a script sorban
        // ellenorzi oket (winget show), es az ELSO LETEZOT hasznalja. Igy
        // egy katalogus-atnevezes nem teszi hasznalhatatlanna a funkciot,
        // es nem is talalgatunk vaktaban.
        private static async Task<EnsureToolJson?> RunEnsureToolAsync(string[] packageIds, string action, Action<string>? log)
        {
            string joined = string.Join(",", packageIds);
            var run = await HwScriptRunner.RunAsync(
                "Ensure-Tool.ps1",
                $"-PackageIds \"{joined}\" -Action {action}",
                log,
                action == "check" ? 120 : 600);

            string output = run.StdOut?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(output)) return null;

            // A script JSON-t ir a standard kimenetre - az elso '{'-tol
            // olvassuk, hogy egy esetleges elozetes sor se zavarjon be.
            int start = output.IndexOf('{');
            if (start < 0) return null;

            try
            {
                using var doc = JsonDocument.Parse(output.Substring(start));
                var root = doc.RootElement;
                return new EnsureToolJson
                {
                    WingetAvailable  = GetBool(root, "winget_available"),
                    Installed        = GetBool(root, "installed"),
                    InstalledVersion = GetString(root, "installed_version"),
                    AvailableVersion = GetString(root, "available_version"),
                    UpdateAvailable  = GetBool(root, "update_available"),
                    Changed          = GetBool(root, "changed"),
                    Message          = GetString(root, "message")
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool GetBool(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el)) return false;
            return el.ValueKind == JsonValueKind.True
                   || (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out bool b) && b);
        }

        private static string GetString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el)) return "";
            return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.ToString();
        }
    }
}
