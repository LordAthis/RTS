using System.Text.Json.Serialization;

namespace RTS.Models
{
    // A modules.json egy bejegyzesenek C# modellje.
    public class ModuleInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        // Rovid, felhasznaloi felulet-baratabb nev (pl. gombfelirat).
        // Ha nincs megadva a modules.json-ban, a "name" mezot hasznaljuk
        // helyette - a mappa/rts-menu.json keresese mindig a "name"-en
        // alapul, ezt a DisplayName nem befolyasolja.
        [JsonPropertyName("display_name")]
        public string? DisplayNameRaw { get; set; }

        public string DisplayName => string.IsNullOrWhiteSpace(DisplayNameRaw) ? Name : DisplayNameRaw!;

        [JsonPropertyName("repo")]
        public string Repo { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("entry_point")]
        public string EntryPoint { get; set; } = "";

        [JsonPropertyName("visibility")]
        public string Visibility { get; set; } = "public";

        [JsonPropertyName("tier")]
        public string Tier { get; set; } = "";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
