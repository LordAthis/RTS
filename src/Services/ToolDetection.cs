// Verzio: v1.1.0 - 2026-09-16
// UJ SZOLGALTATAS (round17) - a LEGFONTOSABB javitas gyokere.
//
// A PROBLEMA, amit megold (LordAthis 2026-09-15/16-i visszajelzese):
// "a GPU-z mar telepitve van, csak nem tudja beolvasni a script, vagy nem
// ellenoriz", illetve "betolti a HDSentinel telepitojet, ami mar regen fel
// van telepitve". A korabbi ToolAcquisition.IsPresent() KIZAROLAG a sajat
// Apps\Tools\<Eszkoz>\ mappajat nezte - ha a felhasznalo maga telepitette
// az eszkozt (Program Files), vagy kezzel letoltotte mashova, arrol az RTS
// SEMMIT nem tudott, ezert ujra es ujra megprobalta beszerezni.
//
// Ez az osztaly EGYETLEN kerdesre valaszol: "hol van ez az eszkoz EZEN a
// gepen?" - es NEGY forrast nez meg, ebben a sorrendben:
//   1. Sajat, hordozhato peldany:      Apps\Tools\<Eszkoz>\*.exe
//   2. Registry "App Paths"            (a Windows sajat program-nyilvantartasa)
//   3. Registry Uninstall-kulcsok      (HKLM 64/32 bit + HKCU) -> InstallLocation
//   4. Ismert, rogzitett utvonalak     (Program Files\...)
//
// SEMMIT nem tolt le, SEMMIT nem telepit es SEMMIT nem indit el - csak
// megnezi, mi van a gepen. A beszerzes kulon felelos (ToolAcquisition).
//
// ROUND18 JAVITAS - "A HDSentinel telepito ismet ketszer probalt elindulni"
// (LordAthis, 2026-09-16). A gyoker-ok: a round16-os hibas letoltes a
// "hdsentinel_setup.zip"-et (azaz a TELEPITOT) csomagolta ki az
// Apps\Tools\HdSentinelFree\ mappaba, es az ott MARADT. A round17-es
// felismeres ezt megtalalta, "mar megvan"-kent jelentette, a lekerdezes
// pedig ezt a TELEPITOT inditotta el a riport-kapcsoloval - amitol
// termeszetesen a telepito-varazslo jott fel. (A naplo is ezt mutatja:
// "A H.D. Sentinel riport-fajl nem jott letre (-r es /REPORT= kapcsolo is
// sikertelen)".)
//
// Javitas: az IsInstallerExecutable() felismeri a telepito-jellegu
// fajlneveket (setup, install, unins, _inst, update...), es az ilyeneket
// a kereses ATUGORJA. Ha egy mappaban CSAK telepito van, az a mappa ugy
// szamit, mintha ures lenne - igy a kereses tovabblep a registry es a
// Program Files agakra, ahol a VALODI, telepitett program van.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace RTS.Services
{
    // Egy eszkoz teljes, gepen talalt allapota.
    public class ToolPresence
    {
        public ToolId Tool { get; set; }

        // Megvan-e barhol a gepen (barmelyik forrasbol).
        public bool Installed => !string.IsNullOrWhiteSpace(ExecutablePath);

        // A megtalalt futtathato fajl teljes utja (null, ha nincs meg).
        public string? ExecutablePath { get; set; }

        // Honnan talaltuk meg - ez jelenik meg a felhasznaloi feluleten is,
        // hogy egyertelmu legyen, miert nem akarja ujra telepiteni.
        // pl. "sajat, hordozhato peldany", "telepitve: C:\Program Files\..."
        public string Source { get; set; } = "";

        // A megtalalt verzio (ha kiolvashato a fajlbol vagy a registrybol).
        public string Version { get; set; } = "";
    }

    // Egy eszkoz statikus leiroja: hogyan ismerjuk fel, es mivel szerezzuk be.
    public class ToolDescriptor
    {
        public ToolId Tool { get; set; }

        // A felhasznaloi feluleten megjeleno nev.
        public string DisplayName { get; set; } = "";

        // winget csomag-azonositok (a beszerzeshez - lasd ToolAcquisition).
        // Ures tomb = ezt az eszkozt nem a winget kezeli.
        //
        // MIERT TOBB AZONOSITO: a winget-katalogus azonositoi idonkent
        // valtoznak vagy atkerulnek mas kiadohoz. Ezert NEM egyetlen,
        // "biztosra vett" azonositot egetunk be: a lista minden elemet
        // sorban ellenorzi a Ensure-Tool.ps1 (winget show), es az ELSO
        // LETEZOT hasznalja. Ha egyik sem letezik, a kod NEM talalgat
        // tovabb, hanem vilagosan jelzi, es felajanlja a kezi telepitest.
        public string[] WingetIds { get; set; } = Array.Empty<string>();

        // Az elsodleges azonosito (naplozashoz, megjelenitesehez).
        public string WingetId => WingetIds.Length > 0 ? WingetIds[0] : "";

        // Reszt vesz-e a hardver-lekerdezesben (van-e ertelme "Uj
        // gyorsjelentes" gombot tenni mellé).
        public bool UsedInReport { get; set; }

        // Az RTS elso indulasakor automatikusan beszerzendo-e. A
        // felhasznalo 2026-09-16-i kerese: az AJANLOTT eszkoz (a
        // LibreHardwareMonitor) keruljon be az automatikusan telepitett
        // modulok koze.
        public bool AutoInstall { get; set; }

        // Rovid magyarazat a feluleten - miert hasznos ez az eszkoz.
        public string Note { get; set; } = "";

        // Az exe fajlnev-mintai (kiterjesztes nelkul, kisbetusen,
        // reszlet-egyezes). Az elso talalat nyer.
        public string[] ExeNames { get; set; } = Array.Empty<string>();

        // A registry Uninstall-kulcsokban szereplo DisplayName mintai
        // (kisbetus reszlet-egyezes).
        public string[] RegistryNames { get; set; } = Array.Empty<string>();

        // Rogzitett, ismert telepitesi helyek (kornyezeti valtozokkal).
        public string[] KnownPaths { get; set; } = Array.Empty<string>();
    }

    public static class ToolDetection
    {
        // ─────────────────────────── Eszkoz-katalogus ───────────────────────────
        // EGY helyen, attekinthetoen - ha egy eszkoz eleresi utja vagy
        // winget-azonositoja valtozik, CSAK ITT kell hozzanyulni.
        public static readonly Dictionary<ToolId, ToolDescriptor> Catalog = new()
        {
            [ToolId.CpuZ] = new ToolDescriptor
            {
                Tool = ToolId.CpuZ,
                DisplayName = "CPU-Z",
                WingetIds = new[] { "CPUID.CPU-Z" },
                ExeNames = new[] { "cpuz", "cpu-z" },
                RegistryNames = new[] { "cpu-z" },
                KnownPaths = new[]
                {
                    @"%ProgramFiles%\CPUID\CPU-Z\cpuz.exe",
                    @"%ProgramFiles(x86)%\CPUID\CPU-Z\cpuz.exe"
                },
                UsedInReport = true,
                AutoInstall = false,
                Note = "Reszletes processzor-adatok. A lekerdezes nelkule is mukodik (WMI)."
            },
            [ToolId.GpuZ] = new ToolDescriptor
            {
                Tool = ToolId.GpuZ,
                DisplayName = "GPU-Z",
                // LordAthis 2026-09-16: a techpowerup Cloudflare + dinamikus
                // szerver-ID miatt NINCS fix letoltesi URL - a winget a
                // hivatalos, tamogatott ut (lasd GPUZ-Silent.md).
                WingetIds = new[] { "TechPowerUp.GPU-Z" },
                ExeNames = new[] { "gpuz", "gpu-z" },
                RegistryNames = new[] { "gpu-z" },
                KnownPaths = new[]
                {
                    @"%ProgramFiles%\GPU-Z\GPU-Z.exe",
                    @"%ProgramFiles(x86)%\GPU-Z\GPU-Z.exe",
                    @"%LOCALAPPDATA%\Programs\GPU-Z\GPU-Z.exe"
                },
                UsedInReport = true,
                AutoInstall = false,
                Note = "Reszletes videokartya-adatok. A lekerdezes nelkule is mukodik (WMI)."
            },
            [ToolId.HdSentinelFree] = new ToolDescriptor
            {
                Tool = ToolId.HdSentinelFree,
                DisplayName = "Hard Disk Sentinel",
                // A HDS-t a SetUpER sajat telepitoje kezeli (AppsList.json,
                // id: "HDS") - ezert itt NINCS winget-azonosito, es a
                // "Telepites" gomb a SetUpER-t hivja (lasd ToolAcquisition).
                WingetIds = Array.Empty<string>(),
                ExeNames = new[] { "hdsentinel", "hard disk sentinel" },
                RegistryNames = new[] { "hard disk sentinel" },
                KnownPaths = new[]
                {
                    @"%ProgramFiles%\Hard Disk Sentinel\HDSentinel.exe",
                    @"%ProgramFiles(x86)%\Hard Disk Sentinel\HDSentinel.exe"
                },
                UsedInReport = true,
                AutoInstall = false,
                Note = "Lemez-egeszseg (kondicio, teljesitmeny, uzemido). Telepiteset a SetUpER vegzi."
            },
            [ToolId.ResourceHacker] = new ToolDescriptor
            {
                Tool = ToolId.ResourceHacker,
                DisplayName = "Resource Hacker",
                WingetIds = new[] { "AngusJohnson.ResourceHacker" },
                ExeNames = new[] { "resourcehacker", "resource_hacker" },
                RegistryNames = new[] { "resource hacker" },
                KnownPaths = new[]
                {
                    @"%ProgramFiles%\Resource Hacker\ResourceHacker.exe",
                    @"%ProgramFiles(x86)%\Resource Hacker\ResourceHacker.exe"
                },
                UsedInReport = false,
                AutoInstall = false,
                Note = "Eroforras-szerkeszto (nem resze a hardver-lekerdezesnek)."
            },

            // ─────────── UJ, round17 (LordAthis 2026-09-16-i kerese) ───────────
            // "Plusz ket program beilleszteni a Csavarkulcs panelre, es az
            //  ajanlottat az automatikusan telepitett modulok koze is vegyuk fel!"
            [ToolId.LibreHardwareMonitor] = new ToolDescriptor
            {
                Tool = ToolId.LibreHardwareMonitor,
                DisplayName = "LibreHardwareMonitor (ajanlott)",
                WingetIds = new[]
                {
                    "LibreHardwareMonitor.LibreHardwareMonitor",
                    "LibreHardwareMonitor"
                },
                ExeNames = new[] { "librehardwaremonitor" },
                RegistryNames = new[] { "libre hardware monitor", "librehardwaremonitor" },
                KnownPaths = new[]
                {
                    @"%ProgramFiles%\LibreHardwareMonitor\LibreHardwareMonitor.exe",
                    @"%ProgramFiles(x86)%\LibreHardwareMonitor\LibreHardwareMonitor.exe",
                    @"%LOCALAPPDATA%\Programs\LibreHardwareMonitor\LibreHardwareMonitor.exe"
                },
                UsedInReport = true,
                // EZ AZ "AJANLOTT" eszkoz: nyilt forrasu, es SAJAT WMI-nevteret
                // (root\LibreHardwareMonitor) publikal, amibol a szenzoradatok
                // (homerseklet, terheles, ventilator, feszultseg) CSENDBEN,
                // ablak-megnyitas nelkul kiolvashatok. Ezert ez kerul be az
                // automatikusan beszerzett eszkozok koze.
                AutoInstall = true,
                Note = "Homerseklet-, ventilator- es terhelesadatok csendes lekerdezese (sajat WMI-nevteren keresztul)."
            },
            [ToolId.HwMonitor] = new ToolDescriptor
            {
                Tool = ToolId.HwMonitor,
                DisplayName = "HWMonitor",
                WingetIds = new[] { "CPUID.HWMonitor" },
                ExeNames = new[] { "hwmonitor" },
                RegistryNames = new[] { "hwmonitor" },
                KnownPaths = new[]
                {
                    @"%ProgramFiles%\CPUID\HWMonitor\HWMonitor.exe",
                    @"%ProgramFiles(x86)%\CPUID\HWMonitor\HWMonitor.exe"
                },
                // FONTOS, OSZINTEN JELEZVE: a HWMonitornak NINCS csendes,
                // parancssoros exportalasa - adatot csak a megnyitott
                // ablakbol, kezzel (F5 -> CSV mentes) lehet kinyerni belole.
                // Ezert a riportban NEM vesz reszt (nincs "Uj gyorsjelentes"
                // gombja), es NEM is telepitjuk automatikusan - a panelen
                // kezzel telepithato es megnyithato eszkozkent szerepel.
                UsedInReport = false,
                AutoInstall = false,
                Note = "Kezi, ablakos szenzor-nezet. Csendes exportra NEM kepes - ahhoz a LibreHardwareMonitor valo."
            }
        };

        public static ToolDescriptor Describe(ToolId tool) => Catalog[tool];

        // ───────────────────────────── Fo belepesi pont ─────────────────────────────
        // Megkeresi az eszkozt a gepen. SOSEM dob kivetelt - ha barmelyik
        // forras hibara fut (pl. registry-jogosultsag), csendben a
        // kovetkezore lep.
        public static ToolPresence Detect(ToolId tool)
        {
            var desc = Describe(tool);
            var presence = new ToolPresence { Tool = tool };

            // 1. Sajat, hordozhato peldany (Apps\Tools\<Eszkoz>\)
            try
            {
                string portableDir = Path.Combine(ToolAcquisition.ToolsDir, tool.ToString());
                string? exe = FindExeIn(portableDir, desc.ExeNames);
                if (exe != null)
                {
                    presence.ExecutablePath = exe;
                    presence.Source = "sajat, hordozhato peldany";
                    presence.Version = ReadFileVersion(exe);
                    return presence;
                }
            }
            catch { }

            // 2. Registry "App Paths" - a Windows sajat program-nyilvantartasa
            try
            {
                foreach (string exeName in desc.ExeNames)
                {
                    string? exe = FromAppPaths(exeName + ".exe");
                    if (exe != null && File.Exists(exe))
                    {
                        presence.ExecutablePath = exe;
                        presence.Source = "telepitve: " + Path.GetDirectoryName(exe);
                        presence.Version = ReadFileVersion(exe);
                        return presence;
                    }
                }
            }
            catch { }

            // 3. Registry Uninstall-kulcsok (HKLM 64/32 bit + HKCU)
            try
            {
                var (dir, version) = FromUninstallKeys(desc.RegistryNames);
                if (dir != null)
                {
                    string? exe = FindExeIn(dir, desc.ExeNames);
                    if (exe != null)
                    {
                        presence.ExecutablePath = exe;
                        presence.Source = "telepitve: " + dir;
                        presence.Version = !string.IsNullOrWhiteSpace(version) ? version : ReadFileVersion(exe);
                        return presence;
                    }
                }
            }
            catch { }

            // 4. Ismert, rogzitett utvonalak
            try
            {
                foreach (string raw in desc.KnownPaths)
                {
                    string full = Environment.ExpandEnvironmentVariables(raw);
                    if (File.Exists(full))
                    {
                        presence.ExecutablePath = full;
                        presence.Source = "telepitve: " + Path.GetDirectoryName(full);
                        presence.Version = ReadFileVersion(full);
                        return presence;
                    }
                }
            }
            catch { }

            presence.Source = "nincs telepitve";
            return presence;
        }

        public static Dictionary<ToolId, ToolPresence> DetectAll()
        {
            var map = new Dictionary<ToolId, ToolPresence>();
            foreach (ToolId tool in Catalog.Keys)
            {
                map[tool] = Detect(tool);
            }
            return map;
        }

        // ───────────────────────────── Segedfuggvenyek ─────────────────────────────
        // Telepito-jellegu fajlnevek. Egy telepito SOHA nem hasznalhato
        // riport-forraskent: a riport-kapcsolot nem ismeri fel, ezert
        // egyszeruen elinditja a telepito-varazslot (lasd a fajl fejlecet).
        private static readonly string[] InstallerNameMarkers =
        {
            "setup", "install", "unins", "_inst", "instal", "update", "updater", "patch"
        };

        public static bool IsInstallerExecutable(string exePath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(exePath)
                    .Replace("-", "").Replace("_", "").Replace(" ", "")
                    .ToLowerInvariant();

                foreach (string marker in InstallerNameMarkers)
                {
                    string m = marker.Replace("_", "");
                    if (name.Contains(m)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static string? FindExeIn(string dir, string[] nameHints)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;
                var all = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    // ROUND18: a telepitoket KISZURJUK - ezek nem tudnak
                    // riportot adni, csak a telepito-varazslot nyitjak meg.
                    .Where(f => !IsInstallerExecutable(f))
                    .ToArray();
                if (all.Length == 0) return null;

                foreach (string hint in nameHints)
                {
                    string needle = hint.Replace("-", "").Replace(" ", "").Replace("_", "").ToLowerInvariant();
                    var matches = all.Where(f =>
                        Path.GetFileNameWithoutExtension(f)
                            .Replace("-", "").Replace(" ", "").Replace("_", "")
                            .ToLowerInvariant()
                            .Contains(needle))
                        .ToArray();
                    if (matches.Length == 0) continue;

                    // ROUND18: 64 bites rendszeren a 64 bites valtozatot
                    // valasztjuk, ha van. A CPU-Z hordozhato csomagjaban pl.
                    // egyszerre van cpuz_x32.exe es cpuz_x64.exe - eddig az
                    // abc-sorrendben elso (x32) nyert, ami 64 bites gepen
                    // kevesebbet lat a hardverbol.
                    if (Environment.Is64BitOperatingSystem)
                    {
                        var x64 = matches.FirstOrDefault(f =>
                            Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains("x64")
                            || Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains("64"));
                        if (x64 != null) return x64;
                    }
                    return matches[0];
                }

                // Szandekosan NEM esunk vissza "az elso barmilyen exe"-re:
                // pontosan ez okozta a round16-os hibat, amikor a
                // kicsomagolt HDSentinel TELEPITOT talalta meg a kod, es azt
                // inditotta el riport-kapcsoloval (mire feljott a
                // telepito-varazslo egy mar telepitett programhoz).
                return null;
            }
            catch { return null; }
        }

        private static string? FromAppPaths(string exeFileName)
        {
            string subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeFileName;
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(subKey);
                        string? path = key?.GetValue("")?.ToString()?.Trim('"');
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static (string? Dir, string Version) FromUninstallKeys(string[] displayNameHints)
        {
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    foreach (string root in roots)
                    {
                        try
                        {
                            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                            using var uninstall = baseKey.OpenSubKey(root);
                            if (uninstall == null) continue;

                            foreach (string name in uninstall.GetSubKeyNames())
                            {
                                try
                                {
                                    using var app = uninstall.OpenSubKey(name);
                                    string display = app?.GetValue("DisplayName")?.ToString() ?? "";
                                    if (string.IsNullOrWhiteSpace(display)) continue;

                                    string lower = display.ToLowerInvariant();
                                    if (!displayNameHints.Any(h => lower.Contains(h.ToLowerInvariant()))) continue;

                                    string version = app?.GetValue("DisplayVersion")?.ToString() ?? "";
                                    string location = app?.GetValue("InstallLocation")?.ToString()?.Trim('"') ?? "";

                                    if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location))
                                        return (location, version);

                                    // Ha nincs InstallLocation, a DisplayIcon-bol
                                    // is kikovetkeztetheto a mappa.
                                    string icon = app?.GetValue("DisplayIcon")?.ToString()?.Trim('"') ?? "";
                                    if (!string.IsNullOrWhiteSpace(icon))
                                    {
                                        string iconPath = icon.Split(',')[0].Trim('"');
                                        string? dir = Path.GetDirectoryName(iconPath);
                                        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                                            return (dir, version);
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
            }
            return (null, "");
        }

        private static string ReadFileVersion(string exePath)
        {
            try
            {
                var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                string v = info.FileVersion ?? info.ProductVersion ?? "";
                return v.Trim();
            }
            catch { return ""; }
        }
    }
}
