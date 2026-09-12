using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RTS.Models
{
    // Verzio v0.4.2 - 2026-09-12
    //
    // ELOKESZITES (VAZLAT) - a felhasznalo kerese alapjan: "Keszitsunk egy
    // LOG feldolgozo egyseget... ha eddig nem volt seged-fajl, hozzunk
    // letre egyet az elso futasnal, amit minden inditaskor ellenoriz."
    //
    // A felhasznalo maga jelezte, hogy a pontos specifikaciot meg meg kell
    // keresnie a sajat jegyzetei kozott, ezert ez EGYELORE CSAK VAZLAT:
    // a ket segedfajl formatuma (allapot + esemenyek) mar letrejon es
    // betoltodik minden inditaskor, de a TENYLEGES feltoltesuk (mikor,
    // milyen "folyamat" kezdodik/folytatodik, milyen "esetek" kerulnek az
    // esemeny-fajlba) meg NINCS kidolgozva - ezt kell majd a jegyzetek
    // alapjan pontositani. Lasd LogProcessor.cs a betoltesi/inditasi
    // logikaert.

    // ".rts-state.json" - RTS gyoker mappajaban. Azt hivatott nyomon
    // kovetni, ha egy tobblepeses folyamat (pl. egy modul tobb lepesben
    // futtatott muvelete, amit egy ujrainditas felbeszakithat) hol tartott,
    // hogy legkozelebb onnan lehessen folytatni.
    public class RtsState
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastCheckedUtc")]
        public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;

        // TODO (jegyzetek alapjan pontositando): milyen folyamatok kerulnek
        // ide, es pontosan mikor irodik/olvasodik ez a lista. Egyelore csak
        // az adatszerkezet keszul el, feltoltes meg sehonnan nem trotenik.
        [JsonPropertyName("pendingProcesses")]
        public List<RtsPendingProcess> PendingProcesses { get; set; } = new();
    }

    public class RtsPendingProcess
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("moduleName")]
        public string ModuleName { get; set; } = "";

        [JsonPropertyName("step")]
        public string Step { get; set; } = "";

        [JsonPropertyName("startedUtc")]
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("note")]
        public string Note { get; set; } = "";
    }

    // "rts-events.json" - RTS gyoker mappajaban. Kulon fajl a fentitol: ez
    // olyan "esetek"/esemenyek naploja, amiket a felhasznalo szerint
    // inditas utan fontos informaciokent erdemes a feluleten megjeleniteni
    // (pl. egy korabbi futas altal eszlelt problema). Egyelore csak
    // adatszerkezet + betoltes/megjelenites-vazlat - a tenyleges
    // esemenyek rogzitesenek helyei (mely modulok, milyen esetekben irjak
    // ezt) meg NINCSENEK kijelolve.
    public class RtsEventLog
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonPropertyName("events")]
        public List<RtsEvent> Events { get; set; } = new();
    }

    public class RtsEvent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "info"; // info | warning | error

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("acknowledged")]
        public bool Acknowledged { get; set; } = false;
    }
}
