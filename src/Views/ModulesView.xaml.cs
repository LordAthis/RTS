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

            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = mod.Name + tierTag + statusTag,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                FontWeight = FontWeights.Bold,
                FontSize = 14
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

            var btn = new Button
            {
                Content = "Futtatas",
                Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                Width = 100,
                Height = 36,
                Tag = mod,
                IsEnabled = mod.Enabled && !string.IsNullOrWhiteSpace(mod.EntryPoint)
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

            var result = ModuleRunner.Run(mod.Name, mod.EntryPoint);
            mainWin.LogToConsole(result.Message);
        }
    }
}
