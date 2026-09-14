// Verzio: v0.5.5 - 2026-09-14
// Kulon fajl (donate.json, az RTS gyokereben) es kulon modell a tamogatas/
// donate tartalomnak - korabban ez az RTS-info.json resze volt
// ("support_title"/"support_lines"/"wallets" mezokkel), most szetvalasztva:
//  - a Home (hazikó) gomb ES induláskor MINDKET fajlt beemeli (RTS-info.json
//    + donate.json), egymas alatt megjelenitve (lasd HomeInfoView.xaml.cs).
//  - a kulon "Donate" gomb (bal-also sarok) CSAK ezt a fajlt tolti be
//    (lasd DonateView.xaml.cs).
// Ugyanazt a "title"+"lines" mintat kovetjuk, mint az RtsInfo-ban, plusz a
// strukturalt "wallets" listat (lasd WalletEntry - QR-kod general hozza,
// Services/QrCodeHelper.cs).
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    public class DonateInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("lines")]
        public List<string> Lines { get; set; } = new();

        [JsonPropertyName("wallets")]
        public List<WalletEntry> Wallets { get; set; } = new();
    }

    public class WalletEntry
    {
        // Megjelenitett nev (pl. "BTC", "Revolut", "Patreon").
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        // Verzio v0.5.7 - 2026-09-14: ez a mezo NEM csak kripto-tarca cimet
        // fogadhat, hanem sima http(s) linket is (pl. Revolut/PayPal/
        // Patreon/Skool) - a megjelenites (DonateContentRenderer) automatikusan
        // felismeri es kattinthato linkkent mutatja, ha URL-nek nez ki.
        [JsonPropertyName("address")]
        public string Address { get; set; } = "";

        // Opcionalis - ha meg van adva (pl. "bitcoin"), a QR-kod tartalma
        // "<scheme>:<address>" (BIP21-stilus, pl. Bitcoin-nal szabvanyos es
        // minden penztarca-app felismeri). Ha ures, a QR csak a nyers cimet
        // kodolja - ez a biztonsagos alapertelmezes olyan ermeknel, ahol
        // nincs altalanosan elterjedt, szabvanyos URI-sema (pl. Flux).
        [JsonPropertyName("uri_scheme")]
        public string UriScheme { get; set; } = "";
    }
}
