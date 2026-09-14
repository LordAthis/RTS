// Verzio: v0.5.5 - 2026-09-14
// A Home (hazikó) kezdolapja - ez toltodik be az A2-be induláskor ES a
// Home gombra kattintva (lasd MainWindow.xaml.cs).
//
// Verzio v0.5.5: MOSTANTOL KETTOT tolt be egyszerre - az RTS-info.json-t
// (altalanos bemutatkozas) ES a donate.json-t (tamogatas/kripto-tarcak),
// egymas alatt. A kulon "Donate" gomb (bal-also sarok) viszont CSAK a
// donate.json-t tolti be onmagaban (lasd DonateView.xaml.cs) - igy
// ugyanaz a donate.json-tartalom ket helyen is megjelenik, de a
// megjelenitesi logika (Services/DonateContentRenderer.cs) csak egyszer
// van megirva.
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
    public partial class HomeInfoView : UserControl
    {
        public HomeInfoView()
        {
            InitializeComponent();
            Load();
        }

        private void Load()
        {
            RootPanel.Children.Clear();
            LoadRtsInfo();
            LoadDonateInfo();
        }

        private void LoadRtsInfo()
        {
            string path = System.IO.Path.Combine(ModuleRunner.FindRtsRoot(), "RTS-info.json");

            if (!File.Exists(path))
            {
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "Nincs meg RTS-info.json a gyokerben. Hozz letre egyet " +
                           "(title/lines mezokkel), majd nyisd meg ujra a Home nezetet.",
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
                var info = JsonSerializer.Deserialize<RtsInfo>(json, options) ?? new RtsInfo();

                if (!string.IsNullOrWhiteSpace(info.Title))
                {
                    RootPanel.Children.Add(new TextBlock
                    {
                        Text = info.Title,
                        Foreground = (Brush)Application.Current.Resources["SecondaryNeon"],
                        FontWeight = FontWeights.Bold,
                        FontSize = 22,
                        Margin = new Thickness(0, 0, 0, 16),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center
                    });
                }

                foreach (var line in info.Lines)
                {
                    var tb = RichTextHelper.BuildTextBlockWithLinks(line, (Brush)Application.Current.Resources["TextBrush"], 14, LogError);
                    tb.Margin = new Thickness(0, 0, 0, 10);
                    RootPanel.Children.Add(tb);
                }
            }
            catch (Exception ex)
            {
                LogError("Hiba az RTS-info.json beolvasasakor: " + ex.Message);
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "Hiba az RTS-info.json beolvasasakor - lasd a logot.",
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
            }
        }

        private void LoadDonateInfo()
        {
            string path = System.IO.Path.Combine(ModuleRunner.FindRtsRoot(), "donate.json");
            if (!File.Exists(path)) return; // nem kotelezo - ha nincs, egyszeruen kimarad

            try
            {
                string json = File.ReadAllText(path);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var donate = JsonSerializer.Deserialize<DonateInfo>(json, options) ?? new DonateInfo();
                DonateContentRenderer.Render(RootPanel, donate, isSubSection: true, onError: LogError);
            }
            catch (Exception ex)
            {
                LogError("Hiba a donate.json beolvasasakor: " + ex.Message);
            }
        }

        private void LogError(string message)
        {
            var mainWin = Application.Current.MainWindow as MainWindow;
            mainWin?.LogToConsole("[HomeInfoView] " + message);
        }
    }
}
