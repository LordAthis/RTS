using System.Windows;
using System.Windows.Controls;
using System.Linq;
using RTS.Services;

namespace RTS.Views
{
    public partial class IWSView : UserControl
    {
        private const string ModuleName = "IWS";

        public IWSView() { InitializeComponent(); }

        public void ApplyOSFilter(string os)
        {
            foreach (var child in ButtonsPanel.Children)
            {
                if (child is Button btn && btn.Tag != null)
                {
                    string[] supported = btn.Tag.ToString()!.Split(',');
                    btn.Visibility = supported.Contains(os) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void BtnExecute(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var mainWin = (MainWindow)Application.Current.MainWindow;
            string? scriptPath = btn.CommandParameter as string;

            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                mainWin.LogToConsole($"[{ModuleName}] Nincs beallitva script ehhez a gombhoz.");
                return;
            }

            if (!ModuleRunner.ModuleInstalled(ModuleName))
            {
                mainWin.LogToConsole($"[{ModuleName}] Nincs telepitve - futtasd eloszor a bootstrap.ps1-et (hianyzik: Apps\\{ModuleName}).");
                return;
            }

            mainWin.LogToConsole($"Vegrehajtas: {btn.Content} (Cel-OS: {mainWin.SelectedOS})");
            var result = ModuleRunner.RunScript(ModuleName, scriptPath);
            mainWin.LogToConsole(result.Message);
        }
    }
}
