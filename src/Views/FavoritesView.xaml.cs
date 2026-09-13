// Verzio: v0.5.0 - 2026-09-13
// Uj nezet: a fejlecben levo villam-ikonos gomb ("Kedvenc feladatok") nyitja
// meg. Betolti a favorites.json-t, feloldja minden tetelt a sajat modulja
// rts-menu.json-ja alapjan (OS-kompatibilitas + "mar megvan" allapot), rovid
// osszefoglalot ir az A1 savba, reszletes listat mutat itt (A2-ben), es
// biztositja a "Futtatas (Osszes Kedvenc Modul)" gombot.
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Models;
using RTS.Services;

namespace RTS.Views
{
    public partial class FavoritesView : UserControl
    {
        public FavoritesView()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh();
        }

        private void Refresh()
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            ItemsPanel.Children.Clear();

            if (!FavoritesRunner.ConfigExists())
            {
                ItemsPanel.Children.Add(new TextBlock
                {
                    Text = "Nincs meg favorites.json a gyokermappaban. Hozz letre egyet " +
                           "(pelda: {\"favorites\":[{\"module\":\"IWS\",\"item_id\":\"core-hardening\"}]}), " +
                           "majd nyisd meg ujra ezt a nezetet.",
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap
                });
                mainWin.SetInfoText("Kedvenc feladatok: nincs meg favorites.json.");
                BtnRunAll.IsEnabled = false;
                return;
            }

            var resolved = FavoritesRunner.ResolveAll(mainWin.SelectedOS);
            int runnable = resolved.Count(r => r.CanRun);
            int alreadyDone = resolved.Count(r => r.AlreadyDone);
            int incompatible = resolved.Count(r => r.Item != null && r.ModuleInstalled && !r.OsCompatible && !r.AlreadyDone);
            int missing = resolved.Count(r => r.Item == null || !r.ModuleInstalled);

            mainWin.SetInfoText(
                $"Kedvenc feladatok: {resolved.Count} tetel | {runnable} futtathato most | " +
                $"{alreadyDone} mar megvan | {incompatible} nem tamogatott ezen az OS-en | {missing} hianyzik/nincs telepitve.");

            BtnRunAll.IsEnabled = runnable > 0;

            foreach (var r in resolved)
            {
                string status = r.Item == null
                    ? "HIANYZIK (torolt/atnevezett tetel)"
                    : !r.ModuleInstalled
                        ? "MODUL NINCS TELEPITVE"
                        : r.AlreadyDone
                            ? "MAR MEGVAN"
                            : !r.OsCompatible
                                ? $"NEM TAMOGATOTT ({mainWin.SelectedOS})"
                                : "FUTTATHATO";

                var row = new Border
                {
                    Background = (Brush)Application.Current.Resources["PanelBgBrush"],
                    BorderBrush = (Brush)Application.Current.Resources["PanelBorderBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(10, 6, 10, 6)
                };
                var text = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = $"{r.Entry.Module} / {(r.Item?.Name ?? r.Entry.ItemId)}  -  {status}"
                };
                row.Child = text;
                ItemsPanel.Children.Add(row);
            }
        }

        private async void BtnRunAll_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            BtnRunAll.IsEnabled = false;
            try
            {
                await FavoritesRunner.RunAllAsync(mainWin.SelectedOS, mainWin.LogToConsole);
            }
            finally
            {
                Refresh();
            }
        }
    }
}
