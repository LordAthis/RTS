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
    public static class RtsVersion
    {
        public const string Version = "0.5.2";
        public const string BuildDate = "2026-09-14";
    }
}
