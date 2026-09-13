// Verzio: v0.5.0 - 2026-09-13
//
// TORTENET: ez a fajl a projekt legelso (regi Gemini-beszelgetesbol atmasolt)
// tervezetebol szarmazik, es evekig VALTOZATLANUL, DE SEHOL NEM HASZNALVA
// allt a repoban (semmi mas fajl nem hivatkozott ra). Az eredeti otlet -
// egy hardcode-olt C# lista, ami eldonti, mikor jelenjen meg egy gomb - nem
// illik az idokozben kialakult, JSON-vezerelt architekturahoz (rts-menu.json,
// lasd RtsMenu.cs / MenuItem), ahol MAR van OS-szures (MenuItem.Os), csak a
// "korabban mar elvegzett muvelet-e" dimenzio hianyzott.
//
// EZERT: a koncepciot (nem a szó szerinti hardcode-olt osztalyt) BEKOTOTTUK -
// a MenuItem kapott egy uj, opcionalis "AlreadyDone" mezot (lasd RtsMenu.cs),
// aminek a tipusa az itt definialt ServiceTaskCondition. Igy NEM kell egy
// masik, parhuzamos, kezzel karbantartott listat vezetni a mar meglevo
// rts-menu.json mellett - minden modul sajat JSON-jaban, tetelenkent lehet
// megadni, mikor tekintjuk "mar megvan"-nak az adott funkciot.
//
// Hasznalat pelda (rts-menu.json-ban, egy MenuItem-en belul):
//   "already_done": {
//     "type": "registry",
//     "path": "HKLM\\SYSTEM\\CurrentControlSet\\Services\\USBSTOR",
//     "name": "Start",
//     "expected_value": "4"
//   }
// Ha a kiertekeles szerint mar megvan, a gomb az RTS feluleten letiltva
// (szurkitve) jelenik meg, "(Mar megvan)" jelzessel - NEM tunik el teljesen,
// hogy a szerviz-technikus lassa: a rendszeren mar alkalmazva van, ne
// tunodjon el, hova tunt a gomb (ugyanaz a mintat kovetjuk, mint a mar
// meglevo "Hamarosan!" (meg nem keszult funkcio) gomboknal).
//
// A tenyleges kiertekelest a Services/ConditionEvaluator.cs vegzi.

using System.Text.Json.Serialization;

namespace RTS.Models
{
    public class ServiceTaskCondition
    {
        // "none" (alapertelmezett, nincs feltetel) | "registry" | "file_exists"
        [JsonPropertyName("type")]
        public string Type { get; set; } = "none";

        // "registry": a kulcs teljes utja (pl. "HKLM\\SYSTEM\\...").
        // "file_exists": a fajl/mappa elerresi utja (kornyezeti valtozokkal,
        //   pl. "%ProgramFiles%\\...", ezeket a kiertekelo feloldja).
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        // Csak "registry" tipusnal: az ertek neve a kulcson belul.
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        // Csak "registry" tipusnal, opcionalis: ha ures, csak azt nezzuk,
        // hogy LETEZIK-e az ertek; ha meg van adva, az ERTEKNEK is egyeznie
        // kell (szoveges osszehasonlitassal, DWORD ertekeknel is stringkent).
        [JsonPropertyName("expected_value")]
        public string ExpectedValue { get; set; } = "";
    }
}
