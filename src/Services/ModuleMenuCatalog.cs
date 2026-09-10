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
    }
}
