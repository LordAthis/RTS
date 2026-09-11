using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RTS.Models;
using RTS.Services;
// A "MenuItem" nev utkozik a System.Windows.Controls.MenuItem-mel (WPF
// beepitett menupont-vezerlo), ezert a sajat rts-menu.json modellt mindig
// ezzel az explicit aliasszal hasznaljuk, sosem csupasz "MenuItem"-kent.
using RtsMenuItem = RTS.Models.MenuItem;

namespace RTS.Views
{
    // Egyseges, adatvezerelt modul-menu nezet: az Apps\<Modul>\rts-menu.json
    // alapjan kategoriankent gombokat rajzol, es a kattintast a MenuRunner-en
    // keresztul, az RTS SAJAT log-paneljebe futtatja - nincs kulon ablak
    // (kiveve a rts-menu.json-ban kifejezetten "requires_console"-kent
    // megjelolt, valoban interaktiv tetelekre).
    public partial class ModuleMenuView : UserControl
    {
        private readonly string _moduleName;
        private RtsMenu? _menu;

        public ModuleMenuView(string moduleName)
        {
            InitializeComponent();
            _moduleName = moduleName;
            // Ez a nezet gorgetheto dobozban jelenik meg, ahol elfer a teljes
            // nev - itt SOSEM roviditunk, a display_name csak a szuk helyu
            // (pl. fejlec-)gomboknak van fenntartva.
            TxtTitle.Text = $"{moduleName} - menu";
            LoadMenu();
        }

        private void LoadMenu()
        {
            CategoriesPanel.Children.Clear();
            _menu = ModuleMenuCatalog.Load(_moduleName);

            if (_menu == null)
            {
                CategoriesPanel.Children.Add(new TextBlock
                {
                    Text = $"Nem talalhato rts-menu.json a(z) {_moduleName} modulhoz.",
                    Foreground = Brushes.OrangeRed,
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var cat in _menu.Categories)
            {
                CategoriesPanel.Children.Add(new TextBlock
                {
                    Text = cat.Name,
                    Foreground = (Brush)Application.Current.Resources["SecondaryNeon"],
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 12, 0, 6)
                });

                var wrap = new WrapPanel();
                foreach (var item in cat.Items)
                {
                    // A gomb felirata TextBlock-kent sortoresre kepesen jelenik meg,
                    // es a gomb MAGASSAGA (nem szelessege) no, ha kell - igy egy
                    // hosszabb tetelnev sem vagodik le/lesz olvashatatlanul kicsi,
                    // barmilyen (akar auto-generalt) nevvel is jon a modulbol.
                    var label = new TextBlock
                    {
                        Text = item.Name,
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        FontSize = 12
                    };
                    var btn = new Button
                    {
                        Content = label,
                        ToolTip = "1 kattintas: leiras. Dupla kattintas: futtatas.",
                        Style = (Style)Application.Current.Resources["NeonButtonStyle"],
                        Width = 210,
                        MinHeight = 46,
                        Padding = new Thickness(6, 4, 6, 4),
                        Margin = new Thickness(0, 0, 8, 8),
                        Tag = item
                    };
                    // Nem a sima Click eseményt hasznaljuk: EGY kattintasra csak a
                    // tetel .md leirasat mutatjuk (nem futtatunk semmit), CSAK dupla
                    // kattintasra hajtjuk vegre tenylegesen - igy a felhasznalo elobb
                    // lathatja, mit csinal egy gomb, mielott futtatna.
                    btn.PreviewMouseLeftButtonDown += Btn_PreviewMouseLeftButtonDown;
                    wrap.Children.Add(btn);
                }
                CategoriesPanel.Children.Add(wrap);
            }

            var mainWin = (MainWindow)Application.Current.MainWindow;
            ApplyOSFilter(mainWin.SelectedOS);
        }

        // Ugyanaz a minta, mint az IWSView-nal: a jelenleg valasztott OS
        // alapjan mutatja/rejti a gombokat.
        public void ApplyOSFilter(string os)
        {
            foreach (var child in CategoriesPanel.Children)
            {
                if (child is not WrapPanel wrap) continue;
                foreach (var w in wrap.Children)
                {
                    if (w is not Button btn || btn.Tag is not RtsMenuItem item) continue;
                    bool supported = item.Os.Contains(os) || (item.OsOverrides?.ContainsKey(os) ?? false);
                    btn.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void Btn_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var btn = (Button)sender;
            var item = (RtsMenuItem)btn.Tag;
            var mainWin = (MainWindow)Application.Current.MainWindow;

            if (e.ClickCount >= 2)
            {
                RunItem(mainWin, item);
            }
            else
            {
                string info = ModuleMenuCatalog.LoadItemInfo(_moduleName, item);
                mainWin.SetInfoText(info);
            }
        }

        private void RunItem(MainWindow mainWin, RtsMenuItem item)
        {
            if (!ModuleRunner.ModuleInstalled(_moduleName))
            {
                mainWin.LogToConsole($"[{_moduleName}] Nincs telepitve - futtasd eloszor a telepitest (F1 -> Modulok ujratelepitese, vagy bootstrap.ps1).");
                return;
            }

            MenuRunner.Execute(_moduleName, item, mainWin.SelectedOS,
                msg => Dispatcher.Invoke(() => mainWin.LogToConsole(msg)));
        }
    }
}
