using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace RTS.Views
{
    public partial class IWSView : UserControl
    {
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
            
            if (btn.Opacity < 1) {
                mainWin.LogToConsole($"[HAMAROSAN] {btn.Content} funkció még fejlesztés alatt.");
                return;
            }

            mainWin.LogToConsole($"Végrehajtás: {btn.Content} (Cél-OS: {mainWin.SelectedOS})");
            // Itt hívjuk meg az IWS repó megfelelő fájlját
        }
    }
}
