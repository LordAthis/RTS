// Verzio: v0.7.0 - 2026-09-15
// ROUND16 PONTOSITAS (LordAthis 2026-09-15-i visszajelzese alapjan): ez a
// kapcsolo MOSTANTOL KIZAROLAG a RustDeskre vonatkozik, NEM az osszes
// segedeszkozre. A CPU-Z / GPU-Z / H.D. Sentinel FREE / Resource Hacker
// mostantol FELTETEL NELKUL, kerdes/kapcsolo nelkul, automatikusan
// beszerzodik minden inditaskor (lasd ToolsBootstrap) - ezek artalmatlan,
// portable, csak-olvaso segedprogramok. A RustDesk viszont tavoli
// hozzaferest ad, ezert MARAD kulon jovahagyashoz kotve: az elso-inditasi
// Igen/Nem kerdes (lasd MainWindow.xaml.cs) es a csavarkulcs-panelen levo,
// KIZAROLAG RustDeskre vonatkozo jelolonegyzet+Alkalmaz allitja ezt a
// mezot ("telepitse ES allitsa be automatikusan a RustDesket").
//
// A beallitas egy egyszeru JSON fajlban lakik a data\ mappaban
// (ModuleRunner.DataDir), NEM a Registry-ben - ez NEM resze a kesobbi
// licenc-vedelemnek, ezert nem kell elrejteni/vedeni, mint a jovobeli
// licenc-szamlalot.
//
// MEGJEGYZES: a korabbi (v0.6.1) valtozat "tools-settings.json" fajlja
// egy MAS jelentesű ("auto_install" = a TELJES eszkoz-csoportra
// vonatkozott) beallitast tartalmazhat mar kikerult gepeken. Csak a JSON
// mezo atnevezese NEM lenne eleg - a HasBeenAsked() a fajl LETEZESET
// nezi, tehat a regi fajl megleteivel a kerdes soha tobbet nem jelenne
// meg, a mogottes ertek pedig csendben false-ra allna (ismeretlen mezo).
// Ezert MOST MAR KULON, UJ FAJLNEVET hasznalunk ("rustdesk-settings.json")
// - a regi "tools-settings.json" egyszeruen figyelmen kivul marad, es a
// meglevo felhasznalok is megkapjak MEG EGYSZER az (uj, RustDesk-specifikus
// szovegű) elso-inditasi kerdest, ami szandekos, mert a mogottes
// viselkedes tenylegesen valtozott.
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTS.Services
{
    public class ToolsSettings
    {
        [JsonPropertyName("rustdesk_auto_install")]
        public bool RustDeskAutoInstall { get; set; } = false;

        // Ha meg soha nem kerdeztuk meg a felhasznalot (nincs meg fajl),
        // ezt a mezot nem is irjuk ki - csak akkor kerul a fajlba, amikor
        // eldolt a valasz (lasd HasAnswer/Save).
        [JsonPropertyName("asked_at_utc")]
        public DateTime AskedAtUtc { get; set; }
    }

    public static class ToolsSettingsService
    {
        private static string SettingsPath => Path.Combine(ModuleRunner.DataDir, "rustdesk-settings.json");

        // Ha ez false, meg SOSEM kerdeztuk meg a felhasznalot - az elso-
        // inditasi popup ekkor jelenik meg (lasd MainWindow.xaml.cs).
        public static bool HasBeenAsked => File.Exists(SettingsPath);

        public static ToolsSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new ToolsSettings();
                string json = File.ReadAllText(SettingsPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<ToolsSettings>(json, options) ?? new ToolsSettings();
            }
            catch
            {
                return new ToolsSettings();
            }
        }

        public static void Save(bool rustDeskAutoInstall)
        {
            try
            {
                var settings = new ToolsSettings { RustDeskAutoInstall = rustDeskAutoInstall, AskedAtUtc = DateTime.UtcNow };
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, options));
            }
            catch
            {
                // Nem kritikus - legrosszabb esetben legkozelebb ujra
                // megkerdezzuk a felhasznalot inditaskor.
            }
        }
    }
}
