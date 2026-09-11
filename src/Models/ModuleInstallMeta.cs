using System;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    // Verzio v0.4.0 - 2026-09-11
    // Minden telepitett modul mappajaban egy ".rts-installed.json" fajlkent
    // taroljuk, hogy PONTOSAN melyik commit-ot toltottuk le - ez teszi
    // lehetove, hogy az RTS kesobb (ujratelepitesnel vagy induláskor) meg
    // tudja allapitani, hogy van-e ujabb valtozat a GitHub-on, ahelyett
    // hogy egy mar letezo mappat felteel nul-kihagyna (ez volt a 9. kor
    // utan jelzett hiba oka: a SetUpER frissitesei nem jutottak el a mar
    // telepitett peldanyhoz).
    public class ModuleInstallMeta
    {
        [JsonPropertyName("commit")]
        public string Commit { get; set; } = "";

        [JsonPropertyName("commitDate")]
        public DateTime? CommitDate { get; set; }

        [JsonPropertyName("installedUtc")]
        public DateTime InstalledUtc { get; set; }

        // "git" vagy "zip" - csak diagnosztikai celra.
        [JsonPropertyName("method")]
        public string Method { get; set; } = "";
    }
}
