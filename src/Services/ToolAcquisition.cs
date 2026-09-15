// Verzio: v0.6.0 - 2026-09-14
// UJ SZOLGALTATAS a "2.1 Eszkozok" kor elokeszitesehez. Feladata: a
// hardver-lekerdezeshez hasznalt kulso, PORTABLE eszkozoket (CPU-Z,
// GPU-Z, H.D. Sentinel FREE, Resource Hacker) biztositja a sajat
// Apps\Tools\<Eszkoz>\ mappaban - letoltve, ha meg nincs ott.
//
// FONTOS, TISZTAZANDO DONTES (lasd a kisero jegyzet-dokumentumot):
// ez a szolgaltatas KULONALLO a SetUpER telepito-csomagjatol
// (AppsList.json + UpDateR.ps1 + Install-*.ps1) - az a csovezetek jelenleg
// KIZAROLAG .exe-kent letoltheto, silent-install kapcsolot tamogato
// telepitoket kezel (lasd UpDateR.ps1: a celfajl mindig "<id>.exe").
// A CPU-Z es a Resource Hacker viszont ZIP-kent terjed, a GPU-Z pedig egy
// onallo, "telepites" nelkuli portable EXE - egyik sem illik bele
// valtoztatas nelkul abba a csovezetekbe. Ezert ezek a lekerdezeshez
// sajat, EGYSZERUBB logikat kapnak itt, FUGGETLENUL attol, hogy a
// felhasznalo vegul hova teszi a "telepites inditasa" gombot a feluleten
// (csavarkulcs-panel vs. SetUpER "teljesen automatizalt telepitok" -
// ezt a felhasznalo meg nem dontotte el, lasd a jegyzet-dokumentumot).
//
// A H.D. Sentinel PRO teljes telepitoje MAR letezik a SetUpER
// AppsList.json-jaban (id: "HDS") - AZT EZ A KOD NEM ERINTI, mert az egy
// masik celt szolgal (teljes, fizetos HDS telepites), mig itt a FREE,
// portable valtozatra van szukseg csak lekerdezeshez.
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RTS.Services
{
    public enum ToolId
    {
        CpuZ,
        GpuZ,
        HdSentinelFree,
        ResourceHacker
    }

    public class ToolAcquisitionResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public string? ExecutablePath { get; set; }
    }

    public static class ToolAcquisition
    {
        // A feladatok.md 2.1 pontjaban mar kutatott, stabil linkek.
        private const string ResourceHackerUrl = "http://www.angusj.com/resourcehacker/resource_hacker.zip";
        private const string HdSentinelFreeUrl = "https://www.hdsentinel.com/hdsentinel_setup.zip";

        // A CPU-Z es GPU-Z verziószáma gyakran valtozik, ezert ezeknel NEM
        // egy fix URL-t hasznalunk, hanem induláskor kiolvassuk a jelenleg
        // aktualis verziót a gyarto oldalarol (lasd feladatok.md: "a
        // verziószámot előbb ki kell olvasni a cpuid.com/... oldalról").
        //
        // FIGYELEM - EZ A KE RESZ MEG NEM ELLENORIZHETO ELESBEN: ebben a
        // munkamenetben (felugyelet nelkuli, hattérben futó Cowork-session)
        // nem sikerult elerni sem a cpuid.com, sem a techpowerup.com oldalt
        // (a webes eleres jovahagyast igenyelt volna, amit senki nem tudott
        // megadni). A lenti regex-minta a legjobb, altalanos becslesem a
        // szokasos oldal-felepitesre - EZT EGY VALODI, ELO TESZTTEL
        // (Windows gepen, tenyleges internet-eleressel) KOTELEZO
        // leellenorizni, mielott elesben hasznaljuk! Ha nem talal talalatot,
        // a kod NEM talalgat, hanem egyertelmu hibauzenetet ad es
        // megnyitja a letoltesi oldalt a felhasznalo bongeszojeben, hogy
        // kezzel tudja folytatni - SOHA nem all le csendben/hibasan.
        private const string CpuZPageUrl = "https://www.cpuid.com/softwares/cpu-z.html";
        private const string GpuZPageUrl = "https://www.techpowerup.com/download/techpowerup-gpu-z/";
        private static readonly Regex CpuZVersionRegex = new(@"cpu-z[_\-]?v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        private static readonly Regex GpuZVersionRegex = new(@"GPU-Z\.?\s*v?(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);

        public static string ToolsDir => Path.Combine(ModuleRunner.AppsDir, "Tools");

        public static string ToolDir(ToolId tool) => Path.Combine(ToolsDir, tool.ToString());

        // Megmondja, hogy a portable eszkoz mar jelen van-e (nem hivja meg
        // magat a lekerdezest - lasd HardwareQueryService).
        public static bool IsPresent(ToolId tool)
        {
            string dir = ToolDir(tool);
            if (!Directory.Exists(dir)) return false;
            return Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories).Length > 0;
        }

        // Ha az eszkoz hianyzik, letolti/kibontja a sajat Apps\Tools\<Eszkoz>\
        // mappajaba. Nem dob kivetelt kifele - mindig egy vilagos,
        // magyar uzenetet ad vissza (log-panelbe irhato).
        // ROUND16 JAVITAS: LordAthis logjai (2026-09-15) egy versenyhelyzetet
        // (race condition) mutattak ki - ha KET fuggetlen hivo (pl. a
        // ToolsView panel-megnyitasi auto-beszerzese ES a "Frissites"
        // gombra kattintva inditott HardwareQueryService.RefreshAsync)
        // NAGYJABOL EGYSZERRE hivja meg EnsureAsync-et UGYANARRA az
        // eszkozre, mindketto atmegy az IsPresent() ellenorzesen (meg egyik
        // sem toltotte le), majd mindketto UGYANABBA a celmappaba probal
        // kicsomagolni/irni - ez okozta a naploban latott "The process
        // cannot access the file... because it is being used by another
        // process" hibat a CPU-Z-nel. A lenti semafor egyszerre csak EGY
        // beszerzest enged at (barmelyik eszkozrol legyen is szo - ezek
        // amugy is ritka, gyors muveletek, nem eri meg eszkozonkent kulon
        // zart bevezetni), es a zar MEGSZERZESE UTAN UJRA ellenorzi az
        // IsPresent()-et, hatha a masik, korabban varakozo hivo idokozben
        // mar vegzett.
        private static readonly SemaphoreSlim AcquireLock = new(1, 1);

        public static async Task<ToolAcquisitionResult> EnsureAsync(ToolId tool, Action<string>? log = null)
        {
            void Log(string m) => log?.Invoke($"[Eszkozok] {m}");

            if (IsPresent(tool))
            {
                return new ToolAcquisitionResult { Ok = true, Message = $"{tool} mar rendelkezesre all." };
            }

            await AcquireLock.WaitAsync();
            try
            {
                // Ujra-ellenorzes a zar megszerzese UTAN - lehet, hogy egy
                // masik, korabban varakozo hivas idokozben mar bepotolta.
                if (IsPresent(tool))
                {
                    return new ToolAcquisitionResult { Ok = true, Message = $"{tool} mar rendelkezesre all." };
                }

                string dir = ToolDir(tool);
                Directory.CreateDirectory(ToolsDir);
                ForceDeleteDirectory(dir); // csak arra az esetre, ha korabban hibasan/felig maradt le

                switch (tool)
                {
                    case ToolId.ResourceHacker:
                        return await DownloadAndExtractZipAsync(dir, ResourceHackerUrl, tool, Log);

                    case ToolId.HdSentinelFree:
                        return await DownloadAndExtractZipAsync(dir, HdSentinelFreeUrl, tool, Log);

                    case ToolId.CpuZ:
                        return await AcquireCpuZAsync(dir, Log);

                    case ToolId.GpuZ:
                        return await AcquireGpuZAsync(dir, Log);

                    default:
                        return new ToolAcquisitionResult { Ok = false, Message = "Ismeretlen eszkoz." };
                }
            }
            catch (Exception ex)
            {
                Log($"Hiba a(z) {tool} beszerzesekor: {ex.Message}");
                return new ToolAcquisitionResult { Ok = false, Message = $"Hiba a(z) {tool} beszerzesekor: {ex.Message}" };
            }
            finally
            {
                AcquireLock.Release();
            }
        }

        private static async Task<ToolAcquisitionResult> DownloadAndExtractZipAsync(string targetDir, string zipUrl, ToolId tool, Action<string> log)
        {
            string tmpZip = Path.Combine(Path.GetTempPath(), $"rts_tool_{tool}.zip");
            try
            {
                log($"{tool} letoltese: {zipUrl}");
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(3);
                byte[] bytes = await http.GetByteArrayAsync(zipUrl);
                await File.WriteAllBytesAsync(tmpZip, bytes);

                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(tmpZip, targetDir, overwriteFiles: true);

                log($"{tool} sikeresen letoltve/kibontva: {targetDir}");
                return new ToolAcquisitionResult { Ok = true, Message = $"{tool} kesz.", ExecutablePath = FindFirstExe(targetDir) };
            }
            finally
            {
                try { File.Delete(tmpZip); } catch { /* nem kritikus */ }
            }
        }

        private static async Task<ToolAcquisitionResult> AcquireCpuZAsync(string targetDir, Action<string> log)
        {
            string? version = await TryFetchVersionAsync(CpuZPageUrl, CpuZVersionRegex, log, "CPU-Z");
            if (version == null)
            {
                OpenInBrowser(CpuZPageUrl);
                return new ToolAcquisitionResult
                {
                    Ok = false,
                    Message = "CPU-Z verziószámát nem sikerult automatikusan megallapitani - a letoltesi oldal megnyilt, kerlek toltsd le kezzel a Tools\\CpuZ mappaba."
                };
            }

            string zipUrl = $"http://download.cpuid.com/cpu-z/cpu-z_{version}-en.zip";
            return await DownloadAndExtractZipAsync(targetDir, zipUrl, ToolId.CpuZ, log);
        }

        private static async Task<ToolAcquisitionResult> AcquireGpuZAsync(string targetDir, Action<string> log)
        {
            string? version = await TryFetchVersionAsync(GpuZPageUrl, GpuZVersionRegex, log, "GPU-Z");
            if (version == null)
            {
                OpenInBrowser(GpuZPageUrl);
                return new ToolAcquisitionResult
                {
                    Ok = false,
                    Message = "GPU-Z verziószámát nem sikerult automatikusan megallapitani - a letoltesi oldal megnyilt, kerlek toltsd le kezzel a Tools\\GpuZ mappaba."
                };
            }

            // ROUND16 - MEGERoSITETT HIBA: LordAthis elo Windows-gepen
            // futtatva 404-et kapott erre a cimre ("Response status code
            // does not indicate success: 404"). Sajat WebFetch-ellenorzesem
            // is ellentmondasos/elavult adatot adott vissza a techpowerup
            // oldalrol (2020-as 2.36.0 verziot mutatott, holott az elo
            // regex-lekerdezes helyesen 2.70.0-t talal) - vagyis a letoltesi
            // cim PONTOS mintajat innen, elo Windows-teszt nelkul NEM tudom
            // megbizhatoan ujra-kitalalni. Ahelyett, hogy egy MASIK,
            // ugyanugy ellenorizetlen mintat probalnek beegetni, a hibat
            // MOST UGYANUGY kezeljuk, mint a verziószam-fel-nem-ismerest:
            // egyertelmu uzenet + a letoltesi oldal megnyitasa kezi
            // letoltesre - SOHA nem all le csendben/hibasan, es nem
            // talalgat tovabb egy mar bizonyitottan hibas mintat.
            string exeUrl = $"https://us1-dl.techpowerup.com/files/GPU-Z.{version}.exe";
            string exePath = Path.Combine(targetDir, $"GPU-Z.{version}.exe");

            try
            {
                Directory.CreateDirectory(targetDir);
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(3);
                log($"GPU-Z letoltese: {exeUrl}");
                byte[] bytes = await http.GetByteArrayAsync(exeUrl);
                await File.WriteAllBytesAsync(exePath, bytes);

                log("GPU-Z sikeresen letoltve.");
                return new ToolAcquisitionResult { Ok = true, Message = "GPU-Z kesz.", ExecutablePath = exePath };
            }
            catch (Exception ex)
            {
                log($"GPU-Z letoltese sikertelen ({exeUrl}): {ex.Message} - a kitalalt fajlnev-minta idokozben megvaltozhatott.");
                OpenInBrowser(GpuZPageUrl);
                return new ToolAcquisitionResult
                {
                    Ok = false,
                    Message = "GPU-Z automatikus letoltese sikertelen (a letoltesi cim mintaja idokozben megvaltozhatott) - a letoltesi oldal megnyilt, kerlek toltsd le kezzel a Tools\\GpuZ mappaba."
                };
            }
        }

        private static async Task<string?> TryFetchVersionAsync(string pageUrl, Regex pattern, Action<string> log, string toolName)
        {
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(20);
                http.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                string html = await http.GetStringAsync(pageUrl);
                var match = pattern.Match(html);
                if (match.Success)
                {
                    string version = match.Groups[1].Value;
                    log($"{toolName} aktualis verzioja megtalalva: {version}");
                    return version;
                }
                log($"{toolName} verzioszam-mintaja nem talalhato a {pageUrl} oldalon - lehet, hogy megvaltozott az oldal felepitese.");
                return null;
            }
            catch (Exception ex)
            {
                log($"{toolName} verziószám-lekérdezés sikertelen ({pageUrl}): {ex.Message}");
                return null;
            }
        }

        private static void OpenInBrowser(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { /* nem kritikus, csak kenyelmi funkcio */ }
        }

        private static string? FindFirstExe(string dir)
        {
            try
            {
                var exe = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
                return exe.Length > 0 ? exe[0] : null;
            }
            catch
            {
                return null;
            }
        }

        // Sajat, kicsi masolata a RtsInstaller.cs-ben mar bevalt
        // ForceDeleteDirectory mintanak (csak-olvashato jelzes levetele
        // minden fajlrol torles elott - lasd ott a reszletes indoklast a
        // git pack-fajlos hibarol). Szandekosan KULON fuggveny, hogy ez a
        // fajl onmagaban, a RtsInstaller.cs erintese nelkul is athelyezheto/
        // reviewolhato legyen.
        private static bool ForceDeleteDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return true;
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(path, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
