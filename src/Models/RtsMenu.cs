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

    // Kulon, konnyu konfig (Apps\<Modul>\rts-repo.json) - SZANDEKOSAN nem
    // resze az RtsMenu-nek: ez a letoltes/telepites viselkedeset szabalyozza
    // (pl. csak a Windows-ag maradjon meg egy tobb-platformos repobol), nem
    // fugg attol, hogy a modulnak van-e mar gomb-szintu rts-menu.json-ja.
    // Igy egy modul kaphat cleanup_dirs-t meg mielott elkeszulne a teljes
    // gomb-szintu menuje, anelkul, hogy ez befolyasolna, hogyan nyilik meg
    // az RTS-ben (lasd ModuleMenuCatalog.HasMenu).
    public class RtsRepoConfig
    {
        // Azoknak a repo-gyoker-mappaknak a listaja, amik CSAK mas
        // platformnak (nem Windows-nak) kellenek - pl. "linux", "mac" -
        // ezeket a telepito/bootstrap letoltes utan torli.
        [JsonPropertyName("cleanup_dirs")]
        public List<string>? CleanupDirs { get; set; }
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

        // Opcionalis: relativ ut egy .md fajlhoz (a modul mappajan belul),
        // ami a tetel reszletes leirasat tartalmazza - EGY kattintasra ezt
        // mutatjuk meg (nem futtatunk semmit), csak DUPLA kattintasra fut
        // ténylegesen a tetel. Ha ures, az alapertelmezett konvenciot
        // hasznaljuk: "rts-info/<id>.md".
        [JsonPropertyName("info_path")]
        public string InfoPath { get; set; } = "";

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
