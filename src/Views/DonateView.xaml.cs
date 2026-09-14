// Verzio: v0.5.8 - 2026-09-14
// Uj nezet: a bal-also sarok "Donate" gombja nyitja meg (lasd
// MainWindow.xaml.cs). CSAK a donate.json tartalmat tolti be - ellentetben
// a HomeInfoView-val, ami az RTS-info.json-t ES a donate.json-t is
// megjeleniti egyutt.
//
// Verzio v0.5.8: InfoFileLoader hasznalata - kulso fajl elsobbseggel,
// hiba/hianyzas eseten csendben beegetett (embedded resource) tartalekra
// esik vissza (lasd Services/InfoFileLoader.cs).
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Models;
using RTS.Services;

namespace RTS.Views
{
    public partial class DonateView : UserControl
    {
        public DonateView()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            RootPanel.Children.Clear();
            var donate = InfoFileLoader.Load<DonateInfo>("donate.json", "RTS.EmbeddedResources.donate.json", LogInfo);

            if (donate == null)
            {
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "Nem sikerult betolteni a donate.json tartalmat (sem kulso, sem beegetett forrasbol) - lasd a logot.",
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                return;
            }

            DonateContentRenderer.Render(RootPanel, donate, isSubSection: false, onError: LogInfo);
        }

        private void LogInfo(string message)
        {
            var mainWin = Application.Current.MainWindow as MainWindow;
            mainWin?.LogToConsole("[DonateView] " + message);
        }
    }
}
