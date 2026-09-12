namespace RTS.Models
{
    // Verzio v0.4.3 - 2026-09-13
    // Az RTS.exe sajat verziojanak EGYETLEN, kozponti helye - minden mas
    // fajl (InfoView, a betoltokepernyo, stb.) innen olvassa ki, igy nem
    // fordulhat elo, hogy elfelejtjuk frissiteni valahol. Minden koronkent,
    // amikor erdemi valtozas tortenik az RTS-ben, ezt a ket erteket kell
    // frissiteni.
    public static class RtsVersion
    {
        public const string Version = "0.4.3";
        public const string BuildDate = "2026-09-13";
    }
}
