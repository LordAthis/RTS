using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RTS.Models;

namespace RTS.Services
{
    // Beolvassa a modules.json-t az RTS gyokermappajabol.
    public static class ModuleCatalog
    {
        public static List<ModuleInfo> Load()
        {
            string path = Path.Combine(ModuleRunner.FindRtsRoot(), "modules.json");
            if (!File.Exists(path))
                return new List<ModuleInfo>();

            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<List<ModuleInfo>>(json, options) ?? new List<ModuleInfo>();
            }
            catch
            {
                // Hibas/serult modules.json eseten ures listat adunk vissza,
                // hogy a UI ne szalljon el - a hivo fel jelzi a felhasznalonak.
                return new List<ModuleInfo>();
            }
        }
    }
}
