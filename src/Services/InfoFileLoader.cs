// Verzio: v0.5.8 - 2026-09-14
// Altalanos betolto: elobb a KULSO fajlt probalja (a telepitett gyokerben,
// pl. RTS-info.json / donate.json) - ha az letezik ES ervenyes JSON, azt
// hasznalja (igy gyors, ujraforditas nelkuli szerkesztes is mukodik). Ha a
// kulso fajl HIANYZIK vagy HIBAS, csendben (csak logolva, nem hibauzenettel
// a felhasznalonak) visszaesik a build-idoben BEEGETETT (embedded resource)
// valtozatra - igy a felulet SOHA nem marad ures/hibas, meg akkor sem, ha
// valaki elrontja a kulso fajlt kezi szerkesztessel.
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace RTS.Services
{
    public static class InfoFileLoader
    {
        // fileName: pl. "RTS-info.json" (a telepitett gyokerben keresi).
        // embeddedLogicalName: pl. "RTS.EmbeddedResources.RTS-info.json"
        //   (lasd RTS.csproj EmbeddedResource LogicalName-je).
        // onLog: opcionalis, informativ (NEM hiba-) uzenetekhez - pl. hogy
        //   melyik forrasbol toltodott be, vagy ha a kulso fajl hibas volt.
        public static T? Load<T>(string fileName, string embeddedLogicalName, Action<string>? onLog = null) where T : class
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            string externalPath = System.IO.Path.Combine(ModuleRunner.FindRtsRoot(), fileName);
            if (File.Exists(externalPath))
            {
                try
                {
                    string json = File.ReadAllText(externalPath);
                    var fromExternal = JsonSerializer.Deserialize<T>(json, options);
                    if (fromExternal != null)
                    {
                        onLog?.Invoke($"{fileName}: kulso fajlbol betoltve.");
                        return fromExternal;
                    }
                }
                catch (Exception ex)
                {
                    onLog?.Invoke($"{fileName}: a kulso fajl hibas ({ex.Message}) - beegetett tartalek hasznalata.");
                }
            }
            else
            {
                onLog?.Invoke($"{fileName}: nincs kulso fajl a gyokerben - beegetett tartalek hasznalata.");
            }

            // Fallback: build-idoben beegetett (embedded) valtozat.
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var stream = asm.GetManifestResourceStream(embeddedLogicalName);
                if (stream == null)
                {
                    onLog?.Invoke($"{fileName}: a beegetett tartalek sem talalhato ({embeddedLogicalName}).");
                    return null;
                }
                using var reader = new StreamReader(stream);
                string embeddedJson = reader.ReadToEnd();
                return JsonSerializer.Deserialize<T>(embeddedJson, options);
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"{fileName}: a beegetett tartalek beolvasasa is hibazott ({ex.Message}).");
                return null;
            }
        }
    }
}
