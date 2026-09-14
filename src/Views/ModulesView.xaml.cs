// Verzio: v0.5.9 - 2026-09-14
// NAGY ATALAKITAS: korabban ez a nezet REPO-kat sorolt fel (pl. "IWS" EGY
// sorkent, egyetlen "Futtatás (Teljes Modul)" gombbal) - a felhasznalo
// ramutatott, hogy egy REPO tenylegesen TOBB, kulon modulbol all (pl. IWS
// 6, DeepSysTools 62 modulbol), es ezt a MOD-listanak tukroznie kellene:
// EGYETLEN, ABC-sorrendbe rendezett listaban, ahol minden SOR egy tenyleges
// modul (nem repo), es 1 kattintasra lefele kinyilva bovebb leirast ad
// (a repo-kontextussal egyutt az A1 savban).
//
// FONTOS KORLATOZAS (osszintan jelezve, nem hallgatva el): jelenleg CSAK
// 3 repohoz (IWS, DeepSysTools, WinRegTools) van meg rts-menu.json a 17
// engedelyezett kozul - a tobbi 14-hez ezt repontent kell majd megirni
// (script-katalogizalas), ez tobb kulon kor munkaja, NEM old meg egy
// lepesben. Addig azok a repok EGYETLEN sorkent jelennek meg (a regi
// mukodes szerint), DE MAR ugyanabban az ABC-sorrendes listaban.
using System;
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
    public partial class ModulesView : UserControl
    {
        // Egy sor a lapos, ABC-sorrendes listaban - vagy egy tenyleges,
        // flattened modul (Item != null), vagy (meg) egy teljes repo
        // (Item == null), ha annak meg nincs rts-menu.json-ja.
        private class FlatEntry
        {
            public string SortKey = "";
            public ModuleInfo Repo = null!;
            public MenuItem? Item;
            public List<MenuItem> Siblings = new(); // ugyanannak a reponak tobbi modulja
        }

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

            var ready = modules.Where(m => m.Enabled).ToList();
            var notReady = modules.Where(m => !m.Enabled).ToList();

            var flat = new List<FlatEntry>();
            foreach (var mod in ready)
            {
                var menu = ModuleMenuCatalog.HasMenu(mod.Name) ? ModuleMenuCatalog.Load(mod.Name) : null;
                if (menu != null && menu.Categories.Count > 0)
                {
                    var allItems = menu.Categories.SelectMany(c => c.Items).ToList();
                    foreach (var item in allItems)
                    {
                        flat.Add(new FlatEntry
                        {
                            SortKey = item.Name,
                            Repo = mod,
                            Item = item,
                            Siblings = allItems
                        });
                    }
                }
                else
                {
                    // Meg nincs rts-menu.json ehhez a repohoz - a regi,
                    // repo-szintu sor marad, MOST MAR az ABC-listaba keverve.
                    flat.Add(new FlatEntry { SortKey = mod.Name, Repo = mod, Item = null });
                }
            }

            foreach (var entry in flat.OrderBy(f => f.SortKey, StringComparer.OrdinalIgnoreCase))
            {
                ModulesPanel.Children.Add(entry.Item != null ? BuildFlattenedItemRow(entry) : BuildModuleRow(entry.Repo));
            }

            if (notReady.Count > 0)
                ModulesPanel.Children.Add(BuildDisabledSection(notReady));
        }

        // UJ v0.5.9: egy TENYLEGES, flattened modul sora (nem repo). Ket
        // gomb: "Futtatás" (CSAK ez az egy modul) es egy kisebb "Teljes repo"
        // (a szulo-repo teljes belepesi pontja - a regi viselkedes). A sorra
        // kattintva LEFELE KINYILIK egy bovebb leiras (ModuleMenuCatalog.
        // LoadItemInfo), es az A1 sav egyidejuleg megmutatja, melyik repobol
        // szarmazik ez a modul, es milyen tovabbi modulok vannak meg abban
        // a repoban.
        private UIElement BuildFlattenedItemRow(FlatEntry entry)
        {
            var item = entry.Item!;
            var repo = entry.Repo;

            var outer = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            var row = new Border
            {
                Background = (Brush)Application.Current.Resources["MainBgBrush"],
                BorderBrush = (Brush)Application.Current.Resources["AccentNeon"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Cursor = Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textPanel = new StackPanel();
            textPanel.Children.Add(new TextBlock
            {
                Text = item.Name,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = $"({repo.Name}) " + item.Description,
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420
            });
            textPanel.Children.Add(new TextBlock
            {
                Text = "(Kattints a sorra a reszletekhez)",
                Foreground = (Brush)Application.Current.Resources["SecondaryNeon"],
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(textPanel, 0);
            grid.Children.Add(textPanel);

            var btnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var btnRunItem = new Button
            {
                Content = "Futtatás",
                Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                Width = 140,
                Height = 32,
                Margin = new Thickness(0, 0, 0, 4)
            };
            btnRunItem.Click += (s, e) =>
            {
                var mainWin = (MainWindow)Application.Current.MainWindow;
                MenuRunner.Execute(repo.Name, item, mainWin.SelectedOS, mainWin.LogToConsole);
            };
            var btnRunFull = new Button
            {
                Content = "Teljes repo",
                Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                Width = 140,
                Height = 26,
                FontSize = 10,
                IsEnabled = !string.IsNullOrWhiteSpace(repo.EntryPoint)
            };
            btnRunFull.Click += (s, e) =>
            {
                var mainWin = (MainWindow)Application.Current.MainWindow;
                if (!ModuleRunner.ModuleInstalled(repo.Name))
                {
                    mainWin.LogToConsole($"[{repo.Name}] Nincs telepitve - futtasd eloszor a bootstrap.ps1-et.");
                    return;
                }
                var result = ModuleRunner.Run(repo.Name, repo.EntryPoint);
                mainWin.LogToConsole(result.Message);
            };
            btnPanel.Children.Add(btnRunItem);
            btnPanel.Children.Add(btnRunFull);
            Grid.SetColumn(btnPanel, 1);
            grid.Children.Add(btnPanel);

            row.Child = grid;

            var detail = new TextBlock
            {
                Visibility = Visibility.Collapsed,
                Foreground = Brushes.LightGray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 8, 14, 0)
            };

            row.MouseLeftButtonUp += (s, e) =>
            {
                bool expand = detail.Visibility != Visibility.Visible;
                detail.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
                if (!expand) return;

                detail.Text = ModuleMenuCatalog.LoadItemInfo(repo.Name, item);

                string siblingNames = string.Join(", ", entry.Siblings.Where(i => i.Id != item.Id).Select(i => i.Name));
                if (string.IsNullOrWhiteSpace(siblingNames)) siblingNames = "(nincs tobbi modul ebben a repoban)";

                var mainWin = (MainWindow)Application.Current.MainWindow;
                mainWin.SetInfoText($"Modul: {item.Name}  |  Alap REPO: {repo.Name} - {repo.Description}  |  Tovabbi modulok ugyanebbol a repobol: {siblingNames}");
            };

            outer.Children.Add(row);
            outer.Children.Add(detail);
            return outer;
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
                Cursor = Cursors.Hand,
                Tag = mod
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string tierTag = mod.Tier == "premium" ? "  [PREMIUM]" : "";

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
                // v0.5.9: jelezve, hogy ez a repo MEG NEM bontott (nincs
                // rts-menu.json) - igy vilagos, miert egyetlen sorkent
                // jelenik meg a tobbi, mar flattened modul kozott.
                Text = "(Meg nincs modulokra bontva - Futtatás (Teljes Modul))",
                Foreground = (Brush)Application.Current.Resources["SecondaryNeon"],
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0)
            });
            Grid.SetColumn(textPanel, 0);
            grid.Children.Add(textPanel);

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
            row.MouseLeftButtonUp += Row_MouseLeftButtonUp;
            return row;
        }

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
