using System.Windows;
using System.Windows.Controls;

namespace RTS.Views
{
    public partial class IWSView : UserControl
    {
        public IWSView()
        {
            InitializeComponent();
        }

        private void BtnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            // Elérjük a főablakot, hogy tudjunk írni a logba
            var mainWin = (MainWindow)Application.Current.MainWindow;

            string taskName = "";
            // Megkeressük a gombban lévő TextBlock-ot a névhez
            if (btn.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.FontWeight == FontWeights.Bold)
                    {
                        taskName = tb.Text;
                    }
                }
            }

            // Kezelés
            if (taskName == "Hálózati Hardening")
            {
                mainWin.LogToConsole("INFO: A Hálózati Hardening funkció jelenleg fejlesztés alatt áll. (Hamarosan!)");
            }
            else
            {
                mainWin.LogToConsole($"Művelet indítása: {taskName}...");
                // Itt hívjuk meg később a .ps1 fájlokat
            }
        }
    }
}
