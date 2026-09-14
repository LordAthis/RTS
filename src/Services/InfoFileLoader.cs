// Verzio: v0.5.9 - 2026-09-14
// FONTOS BIZTONSAGI/BIZALMI JAVITAS (a korabbi v0.5.8 sorrendje FORDITOTT
// volt, es ez kockazatos): a beegetett (embedded resource, build-idoben a
// fejleszto altal, a repo tartalmabol keszult) valtozat az ALAPERTELMEZETT,
// ELSODLEGES forras - ez az EGYETLEN, amit egy tavoli/megosztott gepen a
// fejleszto ténylegesen kontrollal. A kulso fajl (a telepitett gyokerben)
// mostantol csak TARTALEK lehetoseg: ha valaki (fejlesztoi tesztelesre,
// vagy helyi celra) le akarja cserelni, megteheti, DE ez SOHA nem az
// automatikus alapertelmezes.
//
// Az eredeti (v0.5.8) sorrend - kulso fajl elsobbseggel - biztonsagi
// kockazatot jelentett volna: egy tamogatas/donate.json-t tartalmazo
// telepitesnel BARKI, aki hozza fer a helyi fajlhoz, kicserelhette volna
// a fejleszto sajat fizetesi/tarca-linkjeit a sajatjara, es a felhasznalo
// eszre sem venne - a program csendben azt mutatta volna. Emiatt a
// sorrend FORDITVA: beegetett = alapertelmezett, kulso fajl = csak akkor
// szamit, ha a beegetett forras (nem varhato modon) hianyzik vagy hibas.
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace RTS.Services
{
    public static class InfoFileLoader
    {
        // fileName: pl. "RTS-info.json" (a telepitett gyokerben keresi,
        //   CSAK tartalek forraskent, ha a beegetett hianyzik/hibas).
        // embeddedLogicalName: pl. "RTS.EmbeddedResources.RTS-info.json"
        //   (lasd RTS.csproj EmbeddedResource LogicalName-je) - EZ AZ
        //   ALAPERTELMEZETT, ELSODLEGES forras.
        // onLog: opcionalis, informativ (NEM hiba-) uzenetekhez - pl. hogy
        //   melyik forrasbol toltodott be, vagy ha valamelyik hibas volt.
        public static T? Load<T>(string fileName, string embeddedLogicalName, Action<string>? onLog = null) where T : class
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // 1) ELSODLEGES: build-idoben beegetett (embedded) valtozat.
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream(embeddedLogicalName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    string embeddedJson = reader.ReadToEnd();
                    var fromEmbedded = JsonSerializer.Deserialize<T>(embeddedJson, options);
                    if (fromEmbedded != null)
                    {
                        onLog?.Invoke($"{fileName}: beegetett (build-idobeli) tartalombol betoltve.");
                        return fromEmbedded;
                    }
                }
                else
                {
                    onLog?.Invoke($"{fileName}: nincs beegetett tartalom ({embeddedLogicalName}) - ez nem varhato, ellenorizd a .csproj-ot.");
                }
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"{fileName}: a beegetett tartalom beolvasasa hibazott ({ex.Message}).");
            }

            // 2) TARTALEK: kulso fajl a telepitett gyokerben - CSAK akkor
            //    hasznaljuk, ha a beegetett forras (1. lepes) barmiert nem
            //    adott hasznalhato eredmenyt.
            string externalPath = System.IO.Path.Combine(ModuleRunner.FindRtsRoot(), fileName);
            if (File.Exists(externalPath))
            {
                try
                {
                    string json = File.ReadAllText(externalPath);
                    var fromExternal = JsonSerializer.Deserialize<T>(json, options);
                    if (fromExternal != null)
                    {
                        onLog?.Invoke($"{fileName}: tartalek - kulso fajlbol betoltve (a beegetett forras nem volt hasznalhato).");
                        return fromExternal;
                    }
                }
                catch (Exception ex)
                {
                    onLog?.Invoke($"{fileName}: a tartalek kulso fajl is hibas ({ex.Message}).");
                }
            }

            onLog?.Invoke($"{fileName}: sem a beegetett, sem a kulso forras nem adott hasznalhato eredmenyt.");
            return null;
        }
    }
}
