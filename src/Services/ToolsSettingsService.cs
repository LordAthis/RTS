// Verzio: v2.0.0 - 2026-09-16
// ROUND17 BOVITES - a beallito-fajl mostantol a TELJES elso-indulasi
// allapotot rogziti, ahogy LordAthis 2026-09-16-i leirasa kerte:
//
//   "elso indituskor ellenorzi a beallito-fajlt, le vannak-e toltve a
//    komponensek! Mivel nincsen, ezert letolti a komponenseket, de elotte
//    megkerdezi, hogy a Rust-ot is kell-e! (...) elmenti a beallito-fajlba!
//    (JSON: kell-e Rust is, fel vannak-e teve!) (...) Minden indituskor
//    ellenorzi a beallito-fajlbol az allapotot!"
//
// A fajl helye valtozatlanul: <InstallRoot>\data\rustdesk-settings.json
// (a nevet a round16-ban azert kapta, mert akkor MEG csak a RustDesk
// kapcsolot tartalmazta - most tobbet tud, de a nevet SZANDEKOSAN NEM
// valtoztatjuk meg ujra: minden atnevezes azzal jar, hogy az elso-indulasi
// kerdes MEG EGYSZER felugrik a mar mukodo gepeken. A tartalom bovitese
// visszafele kompatibilis: a regi fajl hianyzo mezoi egyszeruen az
// alapertelmezett erteket kapjak.)
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTS.Services
{
    // Egy komponens rogzitett allapota a beallito-fajlban.
    public class ComponentState
    {
        [JsonPropertyName("installed")]
        public bool Installed { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        // Honnan talaltuk meg (sajat mappa / telepitve: ... ) - ez keszult
        // a felhasznaloi felulet szamara, hogy lathato legyen, MIERT nem
        // akarja ujra telepiteni.
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonPropertyName("checked_at_utc")]
        public DateTime? CheckedAtUtc { get; set; }
    }

    public class ToolsSettings
    {
        // Kell-e a RustDesk (ezt kerdezi meg az elso indulasi ablak).
        [JsonPropertyName("rustdesk_auto_install")]
        public bool RustDeskAutoInstall { get; set; } = false;

        // Lefutott-e mar a teljes elso-indulasi beszerzes. Ha true, a
        // kovetkezo indulaskor MAR NEM probalunk semmit beszerezni - csak
        // beolvassuk az elmentett adatokat es megjelenitjuk.
        [JsonPropertyName("components_bootstrapped")]
        public bool ComponentsBootstrapped { get; set; } = false;

        [JsonPropertyName("bootstrapped_at_utc")]
        public DateTime? BootstrappedAtUtc { get; set; }

        // Komponensenkenti allapot (kulcs: a ToolId neve).
        [JsonPropertyName("components")]
        public Dictionary<string, ComponentState> Components { get; set; } = new();

        [JsonPropertyName("asked_at_utc")]
        public DateTime AskedAtUtc { get; set; }
    }

    public static class ToolsSettingsService
    {
        private static string SettingsPath => Path.Combine(ModuleRunner.DataDir, "rustdesk-settings.json");

        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        // Ha ez false, meg SOSEM kerdeztuk meg a felhasznalot - az elso-
        // indulasi popup ekkor jelenik meg (lasd MainWindow.xaml.cs).
        public static bool HasBeenAsked => File.Exists(SettingsPath);

        public static ToolsSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new ToolsSettings();
                string json = File.ReadAllText(SettingsPath);
                if (string.IsNullOrWhiteSpace(json)) return new ToolsSettings();
                return JsonSerializer.Deserialize<ToolsSettings>(json, ReadOptions) ?? new ToolsSettings();
            }
            catch
            {
                return new ToolsSettings();
            }
        }

        public static void Save(ToolsSettings settings)
        {
            try
            {
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, WriteOptions));
            }
            catch
            {
                // Nem kritikus - legrosszabb esetben legkozelebb ujra
                // megkerdezzuk a felhasznalot indulaskor.
            }
        }

        // Visszafele kompatibilis, egyszeru forma (a meglevo hivok miatt):
        // csak a RustDesk-kapcsolot allitja, a tobbi mezot valtozatlanul
        // hagyja.
        public static void Save(bool rustDeskAutoInstall)
        {
            var settings = Load();
            settings.RustDeskAutoInstall = rustDeskAutoInstall;
            settings.AskedAtUtc = DateTime.UtcNow;
            Save(settings);
        }

        // A beszerzes utan a komponensek allapotat is rogzitjuk, hogy a
        // kovetkezo indulas mar csak OLVASSON, ne probaljon beszerezni.
        public static void SaveComponentStates(Dictionary<ToolId, ToolPresence> presences, bool bootstrapped)
        {
            var settings = Load();
            foreach (var kv in presences)
            {
                settings.Components[kv.Key.ToString()] = new ComponentState
                {
                    Installed = kv.Value.Installed,
                    Version = kv.Value.Version,
                    Source = kv.Value.Source,
                    CheckedAtUtc = DateTime.UtcNow
                };
            }
            if (bootstrapped)
            {
                settings.ComponentsBootstrapped = true;
                settings.BootstrappedAtUtc = DateTime.UtcNow;
            }
            Save(settings);
        }
    }
}
