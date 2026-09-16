// Verzio: v2.0.0 - 2026-09-16
// ROUND17 ATALAKITAS - az RTS indulasi folyamata pontosan azt a menetet
// koveti, amit LordAthis 2026-09-16-an leirt:
//
//   1. Elso indulaskor ellenorzi a beallito-fajlt: le vannak-e toltve a
//      komponensek.
//   2. Ha nincsenek: beszerzi oket (a RustDeskre KULON ra kell kerdezni -
//      ezt a MainWindow elso-indulasi ablaka teszi meg, az eredmenyt
//      pedig a beallito-fajl orzi).
//   3. Elmenti a beallito-fajlba (kell-e RustDesk, fel vannak-e teve).
//   4. Lefuttatja a jelentest generalo scriptet CSENDBEN, a HATTERBEN -
//      az ablak betoltese NEM var erre, parhuzamosan futnak.
//   5. Az osszeszedett adatokat egy JSON-ba menti.
//   6. MINDEN kesobbi indulaskor: a beallito-fajlbol latja, hogy minden
//      megvan, ezert MAR NEM SZEREZ BE SEMMIT - csak bekeri az elmentett
//      adatokat (hardware-info.json), es megjeleniti.
//
// A LEGFONTOSABB KULONBSEG a korabbi valtozathoz kepest: a beszerzes
// MOSTANTOL CSAK EGYSZER, az elso indulaskor fut le. Korabban minden
// egyes indulas ES minden panel-megnyitas ujra megprobalta beszerezni a
// hianyzo eszkozoket - ettol ugrott fel ujra meg ujra a techpowerup oldala
// es a HDSentinel telepitoje.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RTS.Services
{
    public static class ToolsBootstrap
    {
        private static bool _running;
        private static readonly object Gate = new();

        // Az RTS indulasakor hivja a MainWindow (fire-and-forget), es a
        // ToolsView panel megnyitasakor is meghivhato. Idempotens: ha mar
        // lefutott (vagy eppen fut), azonnal visszater.
        public static async Task RunSilentlyAsync(Action<string> log)
        {
            lock (Gate)
            {
                if (_running) return;
                _running = true;
            }

            try
            {
                var settings = ToolsSettingsService.Load();

                // ───────── 1-3. lepes: komponensek, CSAK elso indulaskor ─────────
                if (!settings.ComponentsBootstrapped)
                {
                    log("[Eszkozok] Elso indulas: a segedeszkozok allapotanak ellenorzese...");
                    await BootstrapComponentsAsync(settings, log);
                }
                else
                {
                    // Minden kesobbi indulas: CSAK ellenorzunk es rogzitunk -
                    // beszerzes NINCS. Igy ha a felhasznalo kozben maga
                    // telepitett vagy eltavolitott valamit, a lista naprakesz
                    // lesz, de semmi nem indul el a hata mogott.
                    var presences = ToolDetection.DetectAll();
                    ToolsSettingsService.SaveComponentStates(presences, bootstrapped: false);
                }

                // ───────── 4-5. lepes: csendes hatter-lekerdezes ─────────
                // Akkor futtatjuk, ha meg SOSEM volt lekerdezes (elso indulas),
                // vagy ha az elmentett adat mar regi. Egyebkent a mentett
                // JSON-t hasznaljuk (6. lepes) - nem terheljuk feleslegesen
                // a gepet minden indulaskor.
                if (ShouldRefreshOnStartup(log))
                {
                    await HardwareQueryService.RefreshAsync(HwRefreshScope.All, log);
                }
                else
                {
                    log("[Eszkozok] A mentett hardver-adatok naprakeszek - uj lekerdezes nem szukseges.");
                }
            }
            catch (Exception ex)
            {
                log("[Eszkozok] Hiba az indulasi elokeszites soran: " + ex.Message);
            }
            finally
            {
                lock (Gate) { _running = false; }
            }
        }

        // ─────────────────────── Elso indulasi beszerzes ───────────────────────
        private static async Task BootstrapComponentsAsync(ToolsSettings settings, Action<string> log)
        {
            // Eloszor megnezzuk, mi van MAR a gepen (sajat mappa, registry,
            // Program Files) - amit a felhasznalo maga telepitett, azt
            // termeszetesen nem telepitjuk ujra.
            var presences = ToolDetection.DetectAll();

            foreach (var kv in presences)
            {
                var desc = ToolDetection.Describe(kv.Key);
                if (kv.Value.Installed)
                {
                    log($"[Eszkozok] {desc.DisplayName}: mar megvan ({kv.Value.Source}).");
                    continue;
                }

                // CSAK az "ajanlott" (AutoInstall = true) eszkozt szerezzuk
                // be magunktol. LordAthis 2026-09-16: "az ajanlottat az
                // automatikusan telepitett modulok koze is vegyuk fel!" -
                // ez jelenleg a LibreHardwareMonitor, mert sajat WMI-nevteren
                // keresztul CSENDBEN ad homerseklet-/ventilator-adatokat.
                //
                // A tobbi eszkoz NEM telepszik magatol: a hardver-lekerdezes
                // nelkuluk is teljes erteku (WMI-bol dolgozik), es a
                // felhasznalot semmi nem zavarja meg indulaskor. Barmelyik
                // telepitheto kezzel, az Eszkozok panel sajat gombjaval.
                if (!desc.AutoInstall)
                {
                    log($"[Eszkozok] {desc.DisplayName}: nincs telepitve - az Eszkozok panelen, a sor melletti " +
                        "\"Telepites\" gombbal telepitheto (a lekerdezes nelkule is mukodik).");
                    continue;
                }

                var result = await ToolAcquisition.EnsureAsync(kv.Key, log);
                if (!result.Ok)
                {
                    // FONTOS: itt SEMMI nem nyilik meg magatol - se bongeszo,
                    // se telepito. Csak egy log-sor kerul a naploba, es a
                    // felhasznalo dontheti el, telepiti-e kezzel.
                    log($"[Eszkozok] {desc.DisplayName}: automatikus telepites nem sikerult - " +
                        "az Eszkozok panelen kezzel megprobalhato. Reszletek: " + result.Message);
                }
            }

            // RustDesk: KIZAROLAG akkor, ha a felhasznalo ezt kifejezetten
            // keri (elso-indulasi Igen/Nem valasz).
            if (settings.RustDeskAutoInstall && !RustDeskLauncher.IsInstalled() && RustDeskLauncher.IsSetUpErReady())
            {
                var (ok, message) = await RustDeskLauncher.EnsureInstalledAsync(log);
                log(message);
            }

            // 3. lepes: allapot rogzitese a beallito-fajlba - ettol kezdve a
            // kesobbi indulasok MAR NEM szereznek be semmit.
            var after = ToolDetection.DetectAll();
            ToolsSettingsService.SaveComponentStates(after, bootstrapped: true);
            log("[Eszkozok] Az elso indulasi elokeszites befejezodott, az allapot elmentve.");
        }

        // ─────────────────── Kell-e indulaskor uj lekerdezes? ───────────────────
        private static bool ShouldRefreshOnStartup(Action<string> log)
        {
            try
            {
                if (!HardwareQueryService.HasCachedReport()) return true;

                var cached = HardwareQueryService.LoadCached();
                if (cached?.QueriedAtUtc == null) return true;

                // 7 napnal regebbi adat eseten frissitunk - a hardver ritkan
                // valtozik, es igy nem terheljuk feleslegesen a gepet minden
                // indulaskor. (A felhasznalo barmikor kerhet friss adatot az
                // "Informaciok frissitese" gombbal.)
                var age = DateTime.UtcNow - cached.QueriedAtUtc.Value;
                return age.TotalDays >= 7;
            }
            catch
            {
                return true;
            }
        }

        // A ToolsView hasznalja: csak az allapotot frissiti, beszerzes nelkul.
        public static Dictionary<ToolId, ToolPresence> RefreshPresenceStates()
        {
            var presences = ToolDetection.DetectAll();
            ToolsSettingsService.SaveComponentStates(presences, bootstrapped: false);
            return presences;
        }
    }
}
