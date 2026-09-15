// Verzio: v0.6.1 - 2026-09-15
// UJ SZOLGALTATAS - az "automatikus telepites" be/ki kapcsolasat kezeli
// (CPU-Z / GPU-Z / H.D. Sentinel FREE / Resource Hacker / RustDesk
// egysegesen EGY kapcsoloval, ahogy a felhasznalo kerte). A beallitas egy
// egyszeru JSON fajlban lakik a data\ mappaban (ModuleRunner.DataDir),
// NEM a Registry-ben - ez a beallitas ("kerjen-e automatikusan telepiteni
// eszkozoket") NEM resze a kesobbi licenc-vedelemnek, ezert nem kell
// elrejteni/vedeni, mint a jovobeli licenc-szamlalot.
//
// Elso inditaskor (ha meg nincs ez a fajl) az RTS egyszer megkerdezi a
// felhasznalot (lasd MainWindow.xaml.cs), utana MAR NEM kerdez ujra -
// csak akkor valtozik, ha a felhasznalo a csavarkulcs (Eszkozok) panelen
// belul kezzel modositja a jelolonegyzetet + "Alkalmaz" gombot hasznalja.
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTS.Services
{
    public class ToolsSettings
    {
        [JsonPropertyName("auto_install")]
        public bool AutoInstall { get; set; } = false;

        // Ha meg soha nem kerdeztuk meg a felhasznalot (nincs meg fajl),
        // ezt a mezot nem is irjuk ki - csak akkor kerul a fajlba, amikor
        // eldolt a valasz (lasd HasAnswer/Save).
        [JsonPropertyName("asked_at_utc")]
        public DateTime AskedAtUtc { get; set; }
    }

    public static class ToolsSettingsService
    {
        private static string SettingsPath => Path.Combine(ModuleRunner.DataDir, "tools-settings.json");

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

        public static void Save(bool autoInstall)
        {
            try
            {
                var settings = new ToolsSettings { AutoInstall = autoInstall, AskedAtUtc = DateTime.UtcNow };
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
