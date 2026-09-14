// Verzio: v0.5.5 - 2026-09-14
// Uj nezet: a bal-also sarok "Donate" gombja nyitja meg (lasd
// MainWindow.xaml.cs). CSAK a donate.json tartalmat tolti be - ellentetben
// a HomeInfoView-val, ami az RTS-info.json-t ES a donate.json-t is
// megjeleniti egyutt. A tenyleges megjelenitest a megosztott
// Services/DonateContentRenderer.cs vegzi (isSubSection: false - nagyobb
// cim, nincs elvalaszto vonal, mert ez itt az EGYETLEN tartalom).
using System;
using System.IO;
using System.Text.Json;
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
            string path = System.IO.Path.Combine(ModuleRunner.FindRtsRoot(), "donate.json");

            if (!File.Exists(path))
            {
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "Nincs meg donate.json a gyokerben. Hozz letre egyet " +
                           "(title/lines/wallets mezokkel), majd nyisd meg ujra ezt a nezetet.",
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var donate = JsonSerializer.Deserialize<DonateInfo>(json, options) ?? new DonateInfo();
                DonateContentRenderer.Render(RootPanel, donate, isSubSection: false, onError: LogError);
            }
            catch (Exception ex)
            {
                LogError("Hiba a donate.json beolvasasakor: " + ex.Message);
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "Hiba a donate.json beolvasasakor - lasd a logot.",
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
            }
        }

        private void LogError(string message)
        {
            var mainWin = Application.Current.MainWindow as MainWindow;
            mainWin?.LogToConsole("[DonateView] " + message);
        }
    }
}
