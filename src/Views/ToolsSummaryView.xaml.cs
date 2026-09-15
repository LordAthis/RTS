// Verzio: v0.6.0 - 2026-09-14
// DRAFT - lasd ToolsSummaryView.xaml. A B2 dobozba kerul (MainWindow
// konstruktoraban, lasd a kisero jegyzet-dokumentumban a pontos
// beillesztesi helyet).
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
                TxtSummary.Text = "ESZKOZOK\n(meg nincs lekerdezes -\nnyisd meg a 🔧 gombbal)";
                return;
            }

            string cpu = string.IsNullOrWhiteSpace(info.Cpu.Summary) ? "?" : Shorten(info.Cpu.Summary, 22);
            string disk = info.Disk.HealthPercent.HasValue ? $"{info.Disk.HealthPercent}%" : "?";
            string when = info.QueriedAtUtc.HasValue ? info.QueriedAtUtc.Value.ToLocalTime().ToString("MM-dd HH:mm") : "?";

            TxtSummary.Text = $"CPU: {cpu}\nLemez: {disk}\nLekerdezve: {when}";
        }

        private static string Shorten(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
