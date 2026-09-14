// Verzio: v0.5.2 - 2026-09-14
// A B2 ("ESZKOZOK" felirattal induló, jobb oldali) doboz mostantol egy
// kulso, szerkesztheto leiro fajlbol (home-info.json, az RTS gyokereben)
// olvassa ki, mit mutasson - indulaskor ES a Home (hazikó) gombra
// kattintva is ujratoltodik. JSON-t hasznalunk (nem markdown-t), mert:
//  1) a projekt mar mindenhol JSON-t hasznal adatatadasra/konfighoz,
//  2) a B2 egy kicsi doboz - nehany rovid sor eleg, nincs szukseg valodi
//     markdown-rendereszsere (amit a WPF TextBlock amugy sem tud natívan).
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    public class HomeInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("lines")]
        public List<string> Lines { get; set; } = new();
    }
}
