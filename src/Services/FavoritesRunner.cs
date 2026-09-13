// Verzio: v0.5.0 - 2026-09-13
// A "Kedvenc feladatok" (Favorites) betoltese, feloldasa es sorozatban
// tortenlo futtatasa. Lasd Models/Favorites.cs a "miert nem duplikaljuk
// az adatokat" indoklasahoz.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RTS.Models;

namespace RTS.Services
{
    public static class FavoritesRunner
    {
        private static string ConfigPath => Path.Combine(ModuleRunner.FindRtsRoot(), "favorites.json");

        // Verzio v0.5.0: a futasi elozmeny (mikor futott le utoljara egy
        // adott kedvenc tetel) a <InstallRoot>\data\ ala kerul, NEM az exe
        // futtatasi helyere - ld. ModuleRunner.DataDir dokumentacioja.
        private static string StatePath => Path.Combine(ModuleRunner.DataDir, "favorites-state.json");

        public static bool ConfigExists() => File.Exists(ConfigPath);

        public static FavoritesConfig Load()
        {
            if (!File.Exists(ConfigPath))
                return new FavoritesConfig();

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<FavoritesConfig>(json, options) ?? new FavoritesConfig();
            }
            catch
            {
                return new FavoritesConfig();
            }
        }

        // Minden kedvenc tetelt feloldunk a sajat modulja rts-menu.json-ja
        // alapjan - igy mindig a friss OS-lista/already_done erteket latjuk,
        // nem egy elavult masolatot.
        public static List<ResolvedFavorite> ResolveAll(string selectedOs)
        {
            var config = Load();
            var results = new List<ResolvedFavorite>();

            foreach (var entry in config.Favorites)
            {
                var resolved = new ResolvedFavorite { Entry = entry };
                resolved.ModuleInstalled = ModuleRunner.ModuleInstalled(entry.Module);

                var menu = ModuleMenuCatalog.Load(entry.Module);
                MenuItem? found = null;
                if (menu != null)
                {
                    foreach (var cat in menu.Categories)
                    {
                        found = cat.Items.FirstOrDefault(i => i.Id == entry.ItemId);
                        if (found != null) break;
                    }
                }
                resolved.Item = found;

                if (found != null)
                {
                    resolved.OsCompatible = found.Os.Contains(selectedOs) || (found.OsOverrides?.ContainsKey(selectedOs) ?? false);
                    resolved.AlreadyDone = ConditionEvaluator.IsAlreadyDone(found);
                }

                results.Add(resolved);
            }

            return results;
        }

        // Vegigmegy a feloldott listan, MEGHATAROZOTT SORRENDBEN, es csak
        // azokat futtatja, amik "CanRun" - a tobbit naplozza, miert marad ki
        // (nincs telepitve / nem tamogatott OS-en / mar megvan). Minden
        // sikeres futtatas utan frissiti a futasi-elozmeny fajlt.
        public static async Task RunAllAsync(string selectedOs, Action<string> log)
        {
            var resolved = ResolveAll(selectedOs);
            var history = LoadState();

            log($"Kedvenc feladatok futtatasa inditva - {resolved.Count} tetel, sorrendben.");

            int done = 0, skipped = 0;
            foreach (var r in resolved)
            {
                string tag = $"[Kedvenc: {r.Entry.Module}/{r.Entry.ItemId}]";

                if (r.Item == null)
                {
                    log($"{tag} Kihagyva - nem talalhato ez a tetel a modul rts-menu.json-jaban (torolve/atnevezve?).");
                    skipped++;
                    continue;
                }
                if (!r.ModuleInstalled)
                {
                    log($"{tag} Kihagyva - a modul nincs telepitve.");
                    skipped++;
                    continue;
                }
                if (!r.OsCompatible)
                {
                    log($"{tag} Kihagyva - nem tamogatott a jelenlegi OS-en ({selectedOs}).");
                    skipped++;
                    continue;
                }
                if (r.AlreadyDone)
                {
                    log($"{tag} Kihagyva - mar alkalmazva van ezen a rendszeren.");
                    skipped++;
                    continue;
                }

                await MenuRunner.ExecuteAsync(r.Entry.Module, r.Item, selectedOs, log);
                history[$"{r.Entry.Module}/{r.Entry.ItemId}"] = DateTime.Now.ToString("o");
                done++;
            }

            SaveState(history);
            log($"Kedvenc feladatok kesz: {done} lefuttatva, {skipped} kihagyva.");
        }

        private static Dictionary<string, string> LoadState()
        {
            if (!File.Exists(StatePath)) return new Dictionary<string, string>();
            try
            {
                string json = File.ReadAllText(StatePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private static void SaveState(Dictionary<string, string> history)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(StatePath, JsonSerializer.Serialize(history, options));
            }
            catch
            {
                // szandekosan elnyelve - a futasi-elozmeny mentesi hibaja
                // ne akassza meg a tenyleges vegrehajtast
            }
        }
    }
}
