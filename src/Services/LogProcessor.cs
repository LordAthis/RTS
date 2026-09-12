using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using RTS.Models;

namespace RTS.Services
{
    // Verzio v0.4.2 - 2026-09-12
    //
    // ELOKESZITES (VAZLAT) - lasd Models/LogProcessorModels.cs a reszletes
    // magyarazatert. Ez a szolgaltatas minden inditaskor:
    //   1) letrehozza a ket segedfajlt (".rts-state.json", "rts-events.json"),
    //      ha meg nem leteznek az RTS gyoker mappajaban;
    //   2) betolti oket;
    //   3) ha van fel-nem-dolgozott ("pending") folyamat vagy nem-nyugtazott
    //      esemeny, egy rovid osszefoglalot ir a konzolba inditas utan.
    //
    // AMI MEG HIANYZIK (szandekosan, a felhasznalo kerese szerint - "ezeket
    // egyelore keszitsuk elo"): semmilyen modul vagy funkcio meg NEM IR bele
    // ezekbe a fajlokba automatikusan. A tenyleges "mikor inditunk egy
    // folyamatot", "mikor rogzitunk egy esetet" logika a felhasznalo
    // jegyzeteinek elokerulese utan keszul el - ezt kell majd ide, es az
    // egyes modulok/lepesek koze bekotni.
    public static class LogProcessor
    {
        private const string StateFileName = ".rts-state.json";
        private const string EventsFileName = "rts-events.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static RtsState LoadOrCreateState(string rtsRoot)
        {
            string path = Path.Combine(rtsRoot, StateFileName);
            try
            {
                if (!File.Exists(path))
                {
                    var fresh = new RtsState();
                    File.WriteAllText(path, JsonSerializer.Serialize(fresh, JsonOptions));
                    return fresh;
                }

                var state = JsonSerializer.Deserialize<RtsState>(File.ReadAllText(path), JsonOptions) ?? new RtsState();
                state.LastCheckedUtc = DateTime.UtcNow;
                File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
                return state;
            }
            catch
            {
                // Vazlat-allapotban egy sikertelen olvasas/iras nem
                // szabad, hogy megakadalyozza az inditast - ures allapottal
                // folytatjuk.
                return new RtsState();
            }
        }

        public static RtsEventLog LoadOrCreateEvents(string rtsRoot)
        {
            string path = Path.Combine(rtsRoot, EventsFileName);
            try
            {
                if (!File.Exists(path))
                {
                    var fresh = new RtsEventLog();
                    File.WriteAllText(path, JsonSerializer.Serialize(fresh, JsonOptions));
                    return fresh;
                }

                return JsonSerializer.Deserialize<RtsEventLog>(File.ReadAllText(path), JsonOptions) ?? new RtsEventLog();
            }
            catch
            {
                return new RtsEventLog();
            }
        }

        // Inditaskor hivando - letrehozza/betolti a ket fajlt, es ha van
        // barmi emlitesre erdemes bennuk, egy sort ir a konzolba. Vazlat-
        // allapotban ez altalaban ures lesz, mivel meg semmi nem tolti fel
        // oket - de a "gepezet" mar all es minden inditaskor lefut.
        public static void RunStartupCheck(string rtsRoot, Action<string> log)
        {
            var state = LoadOrCreateState(rtsRoot);
            var events = LoadOrCreateEvents(rtsRoot);

            if (state.PendingProcesses.Count > 0)
            {
                log($"[LOG feldolgozo] {state.PendingProcesses.Count} felbeszakadt/folyamatban levo muvelet talalhato - reszletek: {StateFileName}");
                foreach (var p in state.PendingProcesses.Take(5))
                {
                    log($"[LOG feldolgozo]   - {p.ModuleName}: {p.Step} (kezdve: {p.StartedUtc:yyyy-MM-dd HH:mm})");
                }
            }

            var unacknowledged = events.Events.Where(e => !e.Acknowledged).ToList();
            if (unacknowledged.Count > 0)
            {
                log($"[LOG feldolgozo] {unacknowledged.Count} nyugtazatlan esemeny - reszletek: {EventsFileName}");
                foreach (var e in unacknowledged.Take(5))
                {
                    log($"[LOG feldolgozo]   - ({e.Severity}) {e.Category}: {e.Message}");
                }
            }
        }
    }
}
