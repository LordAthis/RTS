// Verzio: v0.5.0 - 2026-09-13
// Kiertekeli egy MenuItem "already_done" feltetelet (lasd Models/ServiceTask.cs
// a tervezesi hattertortenethez). Ket tipust tamogat elsore (registry,
// file_exists) - ez fedi le a legtobb "mar alkalmazva van ez a beallitas"
// esetet a jelenlegi rts-menu.json tetelek kozott. Bovitheto kesobb (pl.
// szolgaltatas-allapot, telepitett program-verzio ellenorzese).
using System;
using System.IO;
using Microsoft.Win32;
using RTS.Models;

namespace RTS.Services
{
    public static class ConditionEvaluator
    {
        // Sosem dob kifele hibat: ha barmi bizonytalan (nem talalhato kulcs,
        // ervenytelen ut, jogosultsag hiany), INKABB "nincs meg elvegezve"-t
        // ad vissza - igy hiba eseten a gomb lathato/futtathato marad, nem
        // tunik el/tiltodik le teves informacio miatt.
        public static bool IsAlreadyDone(MenuItem item)
        {
            var c = item.AlreadyDone;
            if (c == null || string.IsNullOrWhiteSpace(c.Type) || c.Type == "none")
                return false;

            try
            {
                return c.Type switch
                {
                    "file_exists" => CheckFileExists(c),
                    "registry" => CheckRegistry(c),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckFileExists(ServiceTaskCondition c)
        {
            if (string.IsNullOrWhiteSpace(c.Path)) return false;
            string expanded = Environment.ExpandEnvironmentVariables(c.Path);
            return File.Exists(expanded) || Directory.Exists(expanded);
        }

        private static bool CheckRegistry(ServiceTaskCondition c)
        {
            if (string.IsNullOrWhiteSpace(c.Path) || string.IsNullOrWhiteSpace(c.Name))
                return false;

            // A "path" formatuma pl.: "HKLM\SYSTEM\CurrentControlSet\Services\USBSTOR"
            string fullPath = c.Path.Replace('/', '\\').TrimStart('\\');
            int firstSep = fullPath.IndexOf('\\');
            if (firstSep < 0) return false;

            string hiveName = fullPath.Substring(0, firstSep);
            string subKeyPath = fullPath.Substring(firstSep + 1);

            RegistryKey? hive = hiveName.ToUpperInvariant() switch
            {
                "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                "HKU" or "HKEY_USERS" => Registry.Users,
                _ => null
            };
            if (hive == null) return false;

            using var key = hive.OpenSubKey(subKeyPath);
            if (key == null) return false;

            object? value = key.GetValue(c.Name);
            if (value == null) return false;

            // Ha nincs elvart ertek megadva, elegendo, hogy LETEZIK.
            if (string.IsNullOrWhiteSpace(c.ExpectedValue)) return true;

            return string.Equals(value.ToString(), c.ExpectedValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
