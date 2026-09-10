using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    // Egy modul (Apps\<Modul>\rts-menu.json) egyseges menu-lerasa.
    // Ez az UJ, kozos formatum - a modul SAJAT eredeti JSON-jat/PS-menujet
    // nem valtja ki, csak KIEGESZITI, hogy az RTS a sajat feluleten belul
    // tudja megjeleniteni es futtatni az adott modul funkcioit.
    public class RtsMenu
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "1.0";

        [JsonPropertyName("categories")]
        public List<MenuCategory> Categories { get; set; } = new();
    }

    public class MenuCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("items")]
        public List<MenuItem> Items { get; set; } = new();
    }

    public class MenuItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        // "script" (ps1/bat - a kimenet befogva, az RTS sajat log-paneljebe folyik)
        // "shell"  (kozvetlen parancs/CLSID/shell: URI - sajat ablakot/dialogust nyit)
        // "reg"    (.reg fajl csendes alkalmazasa regedit /s-sel)
        [JsonPropertyName("type")]
        public string Type { get; set; } = "script";

        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("command")]
        public string Command { get; set; } = "";

        [JsonPropertyName("fallback_command")]
        public string FallbackCommand { get; set; } = "";

        // Tamogatott RTS OS-kodok: "XP","7","8","10","11"
        [JsonPropertyName("os")]
        public List<string> Os { get; set; } = new();

        // Ha true, NEM befogott/inline futtatas tortenik, hanem kulon,
        // lathato konzolablak nyilik (pl. valodi interaktiv bemenetet var).
        [JsonPropertyName("requires_console")]
        public bool RequiresConsole { get; set; } = false;

        // OS-enkenti felulirasok, ha az adott OS-en mas fajl/tipus kell
        // (pl. XP-n meg .reg, Win10+-on mar .ps1 valtja ki ugyanazt).
        [JsonPropertyName("os_overrides")]
        public Dictionary<string, MenuOsOverride>? OsOverrides { get; set; }
    }

    public class MenuOsOverride
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "script";

        [JsonPropertyName("path")]
        public string Path { get; set; } = "";
    }
}
