using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            // Keszen levo/engedelyezett modulok elore, a "nincs kesz kod/letiltva"
            // modulok legalulra kerulnek, es EGYETLEN kozos "Kinyitas" kapcsolo
            // mogott jelennek meg osszecsukva - nem modulonkent kulon-kulon.
            var ready = modules.Where(m => m.Enabled).ToList();
            var notReady = modules.Where(m => !m.Enabled).ToList();

            foreach (var mod in ready)
                ModulesPanel.Children.Add(BuildModuleRow(mod));

            if (notReady.Count > 0)
                ModulesPanel.Children.Add(BuildDisabledSection(notReady));
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
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = mod
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string tierTag = mod.Tier == "premium" ? "  [PREMIUM]" : "";

            // FONTOS: ez a lista gorgetheto dobozban van, ahol elfer a teljes
            // nev - itt SOSEM roviditunk, a display_name csak a szuk helyu
            // (pl. fejlec-)gomboknak van fenntartva.
            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = mod.Name + tierTag,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = mod.Description,
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 460
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = "(Kattints a sorra a modul megnyitasahoz)",
                Foreground = (Brush)Application.Current.Resources["SecondaryNeon"],
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(textPanel, 0);
            grid.Children.Add(textPanel);

            // A gomb MINDIG a gyors, teljes-modul-inditast vegzi - ez KULON
            // funkcio a sor kattintasatol (ami a modul tartalmat tolti be).
            var btn = new Button
            {
                Content = "Futtatás (Teljes Modul)",
                Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                Width = 150,
                Height = 36,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = mod,
                IsEnabled = !string.IsNullOrWhiteSpace(mod.EntryPoint)
            };
            btn.Click += RunFullModule_Click;
            Grid.SetColumn(btn, 1);
            grid.Children.Add(btn);

            row.Child = grid;
            // Bubble (nem Preview) MouseLeftButtonUp: a Button mar kezeli/elnyeli
            // a sajat kattintasat, igy ez csak akkor tuzel, ha a soron (a
            // gombon KIVUL) kattintottak - pontosan ez a kert viselkedes.
            row.MouseLeftButtonUp += Row_MouseLeftButtonUp;
            return row;
        }

        // EGYETLEN kozos szekcio az OSSZES meg nem kesz/letiltott modulnak -
        // alapertelmezetten csak annyit ir ki, hogy hany ilyen van, plusz egy
        // "Kinyitas" gomb; csak arra kattintva jelenik meg a teljes lista
        // (mindegyik modul neve+leirasa). Nem modulonkent kulon-kulon
        // osszecsukhato - egy kapcsolo mutatja/rejti mindet egyszerre.
        private UIElement BuildDisabledSection(List<ModuleInfo> notReadyModules)
        {
            var outer = new Border
            {
                Background = (Brush)Application.Current.Resources["MainBgBrush"],
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12),
                Opacity = 0.7
            };

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerRow.Children.Add(new TextBlock
            {
                Text = $"Nincs kész kód / letiltva ({notReadyModules.Count} db)",
                Foreground = Brushes.Gray,
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center
            });

            var listPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var mod in notReadyModules)
            {
                var itemPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                itemPanel.Children.Add(new TextBlock
                {
                    Text = mod.Name,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                });
                itemPanel.Children.Add(new TextBlock
                {
                    Text = mod.Description,
                    Foreground = Brushes.Gray,
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                listPanel.Children.Add(itemPanel);
            }
            listPanel.Children.Add(new TextBlock
            {
                Text = "Egyik fenti modulhoz sincs meg kesz, futtathato kod.",
                Foreground = Brushes.OrangeRed,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap
            });

            var toggleBtn = new Button
            {
                Content = "Kinyitás",
                Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                Width = 90,
                Height = 30
            };
            Grid.SetColumn(toggleBtn, 1);
            headerRow.Children.Add(toggleBtn);

            var container = new StackPanel();
            container.Children.Add(headerRow);
            container.Children.Add(listPanel);

            toggleBtn.Click += (s, e) =>
            {
                bool expand = listPanel.Visibility != Visibility.Visible;
                listPanel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
                toggleBtn.Content = expand ? "Összecsukás" : "Kinyitás";
            };

            outer.Child = container;
            return outer;
        }

        // Sorra kattintva a modul TARTALMAT toltjuk be a fo teruletre (ugyanugy,
        // mint egy felso fejlec-gomb) - de itt SOHA nem futtatunk semmit, ez
        // kizarolag a "Futtatás (Teljes Modul)" gomb dolga.
        private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var row = (Border)sender;
            var mod = (ModuleInfo)row.Tag;
            var mainWin = (MainWindow)Application.Current.MainWindow;

            if (ModuleMenuCatalog.HasMenu(mod.Name))
            {
                var menuView = new ModuleMenuView(mod.Name);
                mainWin.SetMainContent(menuView, $"Modul: {mod.Name}");
                menuView.ApplyOSFilter(mainWin.SelectedOS);
            }
            else
            {
                mainWin.SetInfoText($"Modul: {mod.Name} - meg nincs gomb-szintu menuje. Hasznald a 'Futtatás (Teljes Modul)' gombot jobbra a teljes inditasahoz.");
            }
        }

        // A sor melletti gomb: MINDIG a modul teljes, sajat belepesi pontjat
        // futtatja (gyorsmenu), fuggetlenul attol, van-e mar gomb-szintu
        // menuje - ez a sortol elkulonult, onallo funkcio.
        private void RunFullModule_Click(object sender, RoutedEventArgs e)
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
