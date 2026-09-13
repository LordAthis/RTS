// Verzio: v0.5.0 - 2026-09-13
// A "Kedvenc feladatok" (Favorites) automatizacio adatmodellje.
//
// SZANDEKOSAN NEM duplikalja az OS-kompatibilitast vagy a script-utat -
// csak egy (modul, tetel-id) hivatkozast tarol MEGHATAROZOTT SORRENDBEN,
// es a tenyleges reszleteket (os lista, tipus, path, already_done) a modul
// SAJAT rts-menu.json-jabol olvassuk ki futaskor (lasd FavoritesCatalog +
// FavoritesRunner). Igy egyetlen helyen kell karbantartani egy tetel
// adatait, akkor is, ha az szerepel a Kedvencek kozott.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    public class FavoritesConfig
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "1.0";

        // A sorrend SZAMIT - fentrol lefele, ebben a sorrendben fut le
        // a "Futtatas (Osszes Kedvenc Modul)" gombra.
        [JsonPropertyName("favorites")]
        public List<FavoriteEntry> Favorites { get; set; } = new();
    }

    public class FavoriteEntry
    {
        [JsonPropertyName("module")]
        public string Module { get; set; } = "";

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = "";
    }

    // Egy feloldott (modul JSON-jabol beolvasott) kedvenc tetel, a sajat
    // futasi allapotaval egyutt - ezt hasznalja a FavoritesView listazasa
    // es a FavoritesRunner.
    public class ResolvedFavorite
    {
        public FavoriteEntry Entry { get; set; } = new();
        public MenuItem? Item { get; set; }          // null = nem talalhato a modul menujeben
        public bool ModuleInstalled { get; set; }
        public bool OsCompatible { get; set; }
        public bool AlreadyDone { get; set; }

        public bool CanRun => Item != null && ModuleInstalled && OsCompatible && !AlreadyDone;
    }
}
