// Verzio: v0.5.2 - 2026-09-14
// A B2 ("ESZKOZOK" felirattal induló, jobb oldali) doboz mostantol egy
// kulso, szerkesztheto leiro fajlbol (RTS-info.json, az RTS gyokereben)
// olvassa ki, mit mutasson - indulaskor ES a Home (hazikó) gombra
// kattintva is ujratoltodik. JSON-t hasznalunk (nem markdown-t), mert:
//  1) a projekt mar mindenhol JSON-t hasznal adatatadasra/konfighoz,
//  2) a B2 egy kicsi doboz - nehany rovid sor eleg, nincs szukseg valodi
//     markdown-rendereszsere (amit a WPF TextBlock amugy sem tud natívan).
//
// A "support" (tamogatas) kulon, opcionalis szekcio - a fo "lines"-tol
// vizualisan elkulonitve (sajat cim + elvalaszto vonal) jelenik meg, hogy
// jol lathato, de a fo bemutatkozo szoveggel ne folyjon ossze.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    public class RtsInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("lines")]
        public List<string> Lines { get; set; } = new();

        [JsonPropertyName("support_title")]
        public string SupportTitle { get; set; } = "";

        [JsonPropertyName("support_lines")]
        public List<string> SupportLines { get; set; } = new();
    }
}
