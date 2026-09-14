// Verzio: v0.5.5 - 2026-09-14
// A HomeInfoView (A2, Home/hazikó gombra es induláskor) ezt tolti be az
// RTS-info.json-bol - CSAK az altalanos bemutatkozo szoveg. A tamogatas/
// donate resz (korabban ugyanebben a fajlban, "support_title"/
// "support_lines"/"wallets" mezokkel) KULON fajlba (donate.json) es kulon
// modellbe (DonateInfo, lasd DonateInfo.cs) kerult at - igy a Home gomb
// mindkettot beemelheti, a kulon "Donate" gomb pedig CSAK a donate.json-t.
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
    }
}
