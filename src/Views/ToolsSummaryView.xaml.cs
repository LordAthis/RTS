// Verzio: v2.0.0 - 2026-09-16
// ROUND17: a B2 doboz (fo ablak jobb oldala) gyors osszefoglaloja.
// Az uj, v2-es hardware-info.json sema szerint olvas (lasd
// Models/HardwareInfo.cs), es CSAK a mar elmentett eredmenyt jeleniti meg -
// lekerdezest NEM indit. A frissitest az Eszkozok panel "Informaciok
// frissitese" gombja vegzi, ami utana meghivja az itteni Load()-ot.
using System.Windows.Controls;
using RTS.Services;

namespace RTS.Views
{
    public partial class ToolsSummaryView : UserControl
    {
        public ToolsSummaryView()
        {
            InitializeComponent();
            Load();
        }

        public void Load()
        {
            var info = HardwareQueryService.LoadCached();
            if (info == null)
            {
                TxtSummary.Text = "ESZKOZOK\n(meg nincs lekerdezes -\nnyisd meg a \U0001F527 gombbal)";
                return;
            }

            string cpu = info.Cpu != null && !string.IsNullOrWhiteSpace(info.Cpu.Summary)
                ? Shorten(info.Cpu.Summary, 22)
                : "?";

            string disk = info.Disk?.HealthPercent != null
                ? $"{info.Disk.HealthPercent}%"
                : "?";

            string when = info.QueriedAtUtc.HasValue
                ? info.QueriedAtUtc.Value.ToLocalTime().ToString("MM-dd HH:mm")
                : "?";

            string text = $"CPU: {cpu}\nLemez: {disk}";

            // Ha van szenzor-adat (LibreHardwareMonitor), a legmagasabb
            // homersekletet is kiirjuk - ez a leghasznosabb egyetlen szam
            // egy szerviz-helyzetben.
            if (info.Sensors?.Sensors is { Count: > 0 })
            {
                double? maxTemp = null;
                foreach (var s in info.Sensors.Sensors)
                {
                    if (s.Type != "Temperature") continue;
                    if (maxTemp == null || s.Value > maxTemp) maxTemp = s.Value;
                }
                if (maxTemp.HasValue) text += $"\nMax. hom.: {maxTemp.Value:0.#} C";
            }

            text += $"\nLekerdezve: {when}";
            TxtSummary.Text = text;
        }

        private static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "\u2026";
    }
}
