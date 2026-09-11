using System.IO;
using System.Text.Json;
using RTS.Models;

namespace RTS.Services
{
    // Beolvassa egy telepitett modul Apps\<Modul>\rts-menu.json fajljat, ha van.
    public static class ModuleMenuCatalog
    {
        public static bool HasMenu(string moduleName)
        {
            return File.Exists(MenuPath(moduleName));
        }

        public static string MenuPath(string moduleName)
            => Path.Combine(ModuleRunner.AppsDir, moduleName, "rts-menu.json");

        public static RtsMenu? Load(string moduleName)
        {
            string path = MenuPath(moduleName);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<RtsMenu>(json, options);
            }
            catch
            {
                return null;
            }
        }

        // Egy tetel reszletes .md leirasanak beolvasasa - ez jelenik meg
        // EGY kattintasra (a tenyleges futtatas csak DUPLA kattintasra
        // tortenik, lasd ModuleMenuView). Ha nincs kulon info_path megadva,
        // az alapertelmezett "rts-info/<id>.md" konvenciot probalja.
        public static string LoadItemInfo(string moduleName, MenuItem item)
        {
            string relativePath = string.IsNullOrWhiteSpace(item.InfoPath)
                ? System.IO.Path.Combine("rts-info", item.Id + ".md")
                : item.InfoPath;

            string fullPath = System.IO.Path.Combine(ModuleRunner.AppsDir, moduleName, relativePath);
            if (!File.Exists(fullPath))
            {
                // Nincs kulon .md leiras - a rovid JSON-description-t adjuk vissza
                // helyette, hogy legyen legalabb valami visszajelzes.
                return string.IsNullOrWhiteSpace(item.Description)
                    ? $"{item.Name}\n\n(Nincs reszletes leiras ehhez a tetelhez.)"
                    : $"{item.Name}\n\n{item.Description}";
            }

            try
            {
                return File.ReadAllText(fullPath);
            }
            catch
            {
                return $"{item.Name}\n\n(Hiba a leiras beolvasasakor: {fullPath})";
            }
        }
    }
}
