namespace RTS.Models
{
    // Verzio v0.5.0 - 2026-09-13
    // Az RTS.exe sajat verziojanak EGYETLEN, kozponti helye - minden mas
    // fajl (InfoView, a betoltokepernyo, stb.) innen olvassa ki, igy nem
    // fordulhat elo, hogy elfelejtjuk frissiteni valahol. Minden koronkent,
    // amikor erdemi valtozas tortenik az RTS-ben, ezt a ket erteket kell
    // frissiteni.
    //
    // 0.5.0 - uj funkciok: teljes theme-tamogatas (panel/gomb/gorgetosav),
    // A1/A2 layout-javitas, "Eszkozok" gomb+nezet, "Kedvenc feladatok"
    // (Favorites) automatizacio, ServiceTask/MenuItem.AlreadyDone feltetel-
    // rendszer bekotve.
    // 0.5.1 - a verziozsam+datum induláskor a B3 log-dobozba is kiirodik
    // (eddig csak az A1 sávban jelent meg).
    // 0.5.2 - a B2 doboz kulso RTS-info.json leiro fajlbol tolti be a
    // tartalmat, induláskor es a Home gombra kattintva; kulon "tamogatas"
    // szekcioval. A B3 (log) doboz alatti ikon-sor most mar felig belelog
    // a B3 aljaba (mint az A2-nel), a verziozsam+datum pedig a B3-ba is
    // kiirodik induláskor.
    // 0.5.3 - a B2-ben (RTS-info.json "lines"/"support_lines") talalhato
    // http(s):// linkek mostantol kattinthato Hyperlink-kent jelennek meg,
    // a rendszer alapertelmezett bongeszojeben nyilnak meg.
    // 0.5.4 - JAVITAS: az RTS-info.json tartalma innentol az A2-be toltodik
    // (nem a B2-be, ahogy elozo korben tevesen) - uj HomeInfoView, Home
    // gombra es induláskor is ez toltodik be. A B2 visszakerult az eredeti
    // "ESZKOZOK" celjahoz. A B3 (log) doboz keret-RowSpan-nal nyulik le a
    // gombsor moge (nem a gombok mozdulnak fel), igy nagyobb is a doboz.
    // UJ: strukturalt "wallets" lista (label/cim/uri_scheme) - mindegyikhez
    // automatikusan generalt QR-kod (QRCoder csomag).
    // 0.5.5 - a tamogatas/kripto-tarca tartalom KULON fajlba (donate.json)
    // es kulon modellbe (DonateInfo) kerult at az RTS-info.json-bol. A Home
    // gomb MINDKETTOT beemeli (egymas alatt), az uj kulon "Donate" gomb
    // (bal-also sarok, 💰) viszont CSAK a donate.json-t.
    // 0.5.6 - JAVITAS: a jobb oldali gombsor (Eszkozok/LOG/GH/Blog/LinkedIn/
    // Mail) alsó vonala most mar pontosan egybeesik a bal-also lebego
    // gombok (theme/info/donate/exit) alsó vonalaval, meg ha a gombok
    // meretei eltérőek is.
    // 0.5.7 - donate.json bovitve: Revolut, PayPal, Patreon, Skool (linkek,
    // QR-koddal), vegleges sorrend (Revolut legelöl), Flux cim pontositva.
    // A link-tipusu "wallets" tetelek szovege mostantol kattinthato is
    // (nem csak QR), a kripto-cimek maradnak sima, masolhato szoveg.
    // 0.5.8 - JAVITASOK: (1) RTS-info.json/donate.json tartalma build-
    // idoben BEEGETVE (embedded resource), kulso fajl csak opcionalis
    // felulbiralas - hiba/hianyzas eseten csendben az beegetett valtozatra
    // esik vissza, sosem marad ures/hibas a felulet; (2) a "lines" elso
    // eleme fejlec-szeruen (kozepen, felkover), a tobbi balra igazitva
    // jelenik meg; (3) a szoveg ekezetesen (WPF UI, nem konzol - a
    // "kiirt szoveg ekezetmentes" szabaly a parancssorra vonatkozik);
    // (4) B3 atlogas javitva FELIG-re (nem a teljes gombsor moge, csak
    // a fele - a v0.5.4-es RowSpan tulsokat takart a log-szovegbol).
    public static class RtsVersion
    {
        public const string Version = "0.5.8";
        public const string BuildDate = "2026-09-14";
    }
}
