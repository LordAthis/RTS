using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Models;
using RTS.Services;

namespace RTS.Views
{
    // Altalanos modul-lista nezet: minden modules.json-beli bejegyzest
    // felsorol egy "Futtatas" gombbal - igy uj modulhoz nem kell kulon UI-t
    // irni, eleg a modules.json-t bovíteni.
    public partial class ModulesView : UserControl
    {
        public ModulesView()
        {
            InitializeComponent();
            LoadModules();
        }

        private void LoadModules()
        {
            ModulesPanel.Children.Clear();
            var modules = ModuleCatalog.Load();

            if (modules.Count == 0)
            {
                ModulesPanel.Children.Add(new TextBlock
                {
                    Text = "Nem talalhato modules.json - futtasd az RTS gyokermappajabol!",
                    Foreground = Brushes.OrangeRed,
                    Margin = new Thickness(5),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var mod in modules)
            {
                ModulesPanel.Children.Add(BuildModuleRow(mod));
            }
        }

        private UIElement BuildModuleRow(ModuleInfo mod)
        {
            var row = new Border
            {
                Background = (Brush)Application.Current.Resources["MainBgBrush"],
                BorderBrush = (Brush)Application.Current.Resources["AccentNeon"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12),
                Opacity = mod.Enabled ? 1.0 : 0.45
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string tierTag = mod.Tier == "premium" ? "  [PREMIUM]" : "";
            string statusTag = mod.Enabled ? "" : "  (nincs kesz kod / letiltva)";

            // FONTOS: ez a lista gorgetheto dobozban van, ahol elfer a teljes
            // nev - itt SOSEM roviditunk, a display_name csak a szuk helyu
            // (pl. fejlec-)gomboknak van fenntartva.
            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = mod.Name + tierTag + statusTag,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 480
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = mod.Description,
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 480
            });
            Grid.SetColumn(textPanel, 0);
            grid.Children.Add(textPanel);

            bool hasMenu = ModuleMenuCatalog.HasMenu(mod.Name);

            var btn = new Button
            {
                // Ha van gomb-szintu rts-menu.json a modulhoz, azt nyitjuk meg
                // az RTS sajat feluleten belul; kulonben (meg nincs feldolgozva)
                // a modul teljes sajat belepesi pontja indul, vegso esetkent.
                Content = hasMenu ? "Menü megnyitása" : "Futtatas (teljes modul)",
                Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                Width = 150,
                Height = 36,
                Tag = mod,
                IsEnabled = mod.Enabled && (hasMenu || !string.IsNullOrWhiteSpace(mod.EntryPoint))
            };
            btn.Click += Btn_Click;
            Grid.SetColumn(btn, 1);
            grid.Children.Add(btn);

            row.Child = grid;
            return row;
        }

        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            var btn = (Button)sender;
            var mod = (ModuleInfo)btn.Tag;
            var mainWin = (MainWindow)Application.Current.MainWindow;

            if (!ModuleRunner.ModuleInstalled(mod.Name))
            {
                mainWin.LogToConsole($"[{mod.Name}] Nincs telepitve - futtasd eloszor a bootstrap.ps1-et (hianyzik: Apps\\{mod.Name}).");
                return;
            }

            if (ModuleMenuCatalog.HasMenu(mod.Name))
            {
                // A menu-nezet is gorgetheto doboz - itt is a teljes nev jar.
                var menuView = new ModuleMenuView(mod.Name);
                mainWin.SetMainContent(menuView, $"Modul: {mod.Name}");
                menuView.ApplyOSFilter(mainWin.SelectedOS);
                return;
            }

            mainWin.LogToConsole($"[{mod.Name}] Meg nincs gomb-szintu menuje - a modul sajat teljes belepesi pontja indul, vegso esetkent.");
            var result = ModuleRunner.Run(mod.Name, mod.EntryPoint);
            mainWin.LogToConsole(result.Message);
        }
    }
}
