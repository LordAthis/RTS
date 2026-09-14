// Verzio: v0.5.8 - 2026-09-14
// A Home (hazikó) kezdolapja - ez toltodik be az A2-be induláskor ES a
// Home gombra kattintva (lasd MainWindow.xaml.cs). Betolti az RTS-info.json-t
// ES a donate.json-t is (lasd Models/RtsInfo.cs, Models/DonateInfo.cs).
//
// Verzio v0.5.8 - KET valtozas:
//  1) InfoFileLoader hasznalata - kulso fajl elsobbseggel, hiba/hianyzas
//     eseten csendben beegetett (embedded resource) tartalekra esik vissza,
//     igy SOSEM marad ures/hibas a felulet (korabban ez tortent, ha a
//     kulso RTS-info.json serult volt).
//  2) A "lines" tomb ELSO eleme fejlec-szeruen (kozepen, felkover, kicsit
//     nagyobb betu), a TOBBI bal-igazitva, normal sullyal jelenik meg -
//     igy a hosszabb, tobb bekezdesre tordelt szoveg olvashatobb.
using System;
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
            var info = InfoFileLoader.Load<RtsInfo>("RTS-info.json", "RTS.EmbeddedResources.RTS-info.json", LogInfo);
            if (info == null)
            {
                RootPanel.Children.Add(new TextBlock
                {
                    Text = "Nem sikerult betolteni az RTS-info.json tartalmat (sem kulso, sem beegetett forrasbol) - lasd a logot.",
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                return;
            }

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

            AddParagraphs(RootPanel, info.Lines, LogInfo);
        }

        // Verzio v0.5.8 - megosztott logika: az ELSO bekezdes fejlec-szeruen
        // (kozepre igazitva, felkover, 15px), a TOBBI balra igazitva,
        // normal sullyal (14px) jelenik meg. Ugyanezt hasznalja a
        // DonateContentRenderer is a donate.json "lines" tombjehez.
        internal static void AddParagraphs(Panel target, System.Collections.Generic.List<string> lines, Action<string>? onError)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                bool isHeader = i == 0;
                var tb = RichTextHelper.BuildTextBlockWithLinks(lines[i], (Brush)Application.Current.Resources["TextBrush"], isHeader ? 15 : 14, onError);
                tb.TextAlignment = isHeader ? TextAlignment.Center : TextAlignment.Left;
                tb.FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal;
                tb.Margin = new Thickness(0, 0, 0, isHeader ? 12 : 8);
                target.Children.Add(tb);
            }
        }

        private void LoadDonateInfo()
        {
            var donate = InfoFileLoader.Load<DonateInfo>("donate.json", "RTS.EmbeddedResources.donate.json", LogInfo);
            if (donate == null) return; // nem kotelezo - ha egyik forrasbol sem toltheto, egyszeruen kimarad
            DonateContentRenderer.Render(RootPanel, donate, isSubSection: true, onError: LogInfo);
        }

        private void LogInfo(string message)
        {
            var mainWin = Application.Current.MainWindow as MainWindow;
            mainWin?.LogToConsole("[HomeInfoView] " + message);
        }
    }
}
