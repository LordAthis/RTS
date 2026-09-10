using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RTS.Services;
using RTS.Views;   // IWSView miatt

namespace RTS
{
    public partial class MainWindow : Window
    {
        private bool isDark = true;
        private Button? activeOSButton = null;
        public string SelectedOS { get; private set; } = "10";   // alapértelmezett

        public MainWindow()
        {
            InitializeComponent();
            DetectCurrentOS();
            LogToConsole("NEXUS RTS Rendszer betöltve. Keretrendszer készen áll.");
            EnsureRtsInstalled();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        // ─────────────────────────────────────────────
        //  ELSO-INDITASI TELEPITO
        // ─────────────────────────────────────────────
        private void EnsureRtsInstalled()
        {
            string root = ModuleRunner.FindRtsRoot();
            if (System.IO.File.Exists(System.IO.Path.Combine(root, "modules.json")))
            {
                LogToConsole("RTS adatmappa megtalalva: " + root);
                return;
            }

            LogToConsole("Elso inditas - modules.json nem talalhato. Telepitest kell futtatni.");
            string? chosen = RtsInstaller.PromptForInstallFolder();
            if (chosen == null)
            {
                LogToConsole("Telepites megszakitva - a modulok nem lesznek elerhetok, amig ujra nem inditod es nem valasztasz mappat.");
                return;
            }

            LogToConsole($"Telepites inditasa ide: {chosen}");
            Task.Run(() =>
            {
                RtsInstaller.RunFirstTimeSetup(chosen, msg => Dispatcher.Invoke(() => LogToConsole(msg)));
                Dispatcher.Invoke(() =>
                {
                    ModuleRunner.ResetRootCache();
                    LogToConsole("Telepites befejezve - nyisd meg ujra a Modulok nezetet a friss allapothoz.");
                });
            });
        }

        // ─────────────────────────────────────────────
        //  ABLAK-ATMERETEZES (WindowStyle="None" + AllowsTransparency
        //  miatt a beepitett resize-grip nem mukodik, ezert kulon
        //  kezeljuk a WM_SYSCOMMAND / SC_SIZE uzenettel)
        // ─────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_SYSCOMMAND = 0x112;

        private static readonly System.Collections.Generic.Dictionary<string, int> ResizeDirections = new()
        {
            { "Left", 61441 }, { "Right", 61442 }, { "Top", 61443 },
            { "TopLeft", 61444 }, { "TopRight", 61445 }, { "Bottom", 61446 },
            { "BottomLeft", 61447 }, { "BottomRight", 61448 }
        };

        private void ResizeWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            string? direction = (sender as FrameworkElement)?.Tag as string;
            if (direction == null || !ResizeDirections.TryGetValue(direction, out int dirCode)) return;

            ReleaseCapture();
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            SendMessage(hwnd, WM_SYSCOMMAND, dirCode, 0);
        }

        private void DetectCurrentOS()
        {
            var os = Environment.OSVersion;
            int major = os.Version.Major;
            int minor = os.Version.Minor;
            int build = os.Version.Build;
                
            if (major == 5) 
                HighlightOSButton(BtnXP);           // Windows XP
            else if (major == 6 && minor == 1) 
                HighlightOSButton(BtnWin7);         // Windows 7
            else if (major == 10 && build < 22000) 
                HighlightOSButton(BtnWin10);        // Windows 10
            else if (major == 10 && build >= 22000) 
                HighlightOSButton(BtnWin11);        // Windows 11
            else 
                HighlightOSButton(BtnWin10);        // fallback
        }

        public void HighlightOSButton(Button target)
        {
            if (target == null) return;

            if (activeOSButton != null)
            {
                activeOSButton.Background = Brushes.Transparent;
                activeOSButton.Foreground = (SolidColorBrush)Application.Current.Resources["TextBrush"];
            }

            target.Background = (SolidColorBrush)Application.Current.Resources["AccentNeon"];
            target.Foreground = Brushes.Black;
            activeOSButton = target;
            SelectedOS = target.Content.ToString() ?? "10";

            if (MainContentArea.Content is IWSView iws)
                iws.ApplyOSFilter(SelectedOS);
            else if (MainContentArea.Content is Views.ModuleMenuView menuView)
                menuView.ApplyOSFilter(SelectedOS);
        }

        // Mas nezetek (pl. ModulesView) innen tudjak kicserelni a fo tartalmat,
        // anelkul, hogy a MainContentArea/TxtInfo mezokhoz kozvetlenul
        // hozzáférnének.
        public void SetMainContent(UIElement content, string infoText)
        {
            MainContentArea.Content = content;
            TxtInfo.Text = infoText;
        }

        public void LogToConsole(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.Text += $"[{time}] {message}\n";
            LogScroller.ScrollToBottom();
        }

        private void Module_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            switch (btn.Name)
            {
                case "BtnRTS":   // F1 - rendszerinfo / gyorsinditó
                    var infoView = new InfoView();
                    MainContentArea.Content = infoView;
                    TxtInfo.Text = "RTS - Rendszerinfo";
                    break;

                case "BtnIWS":
                    // Uj, egyseges menu-nezet, ha van rts-menu.json a modulhoz;
                    // kulonben visszaesunk a regi, kezzel irt IWSView-ra.
                    if (ModuleMenuCatalog.HasMenu("IWS"))
                    {
                        var iwsMenuView = new Views.ModuleMenuView("IWS");
                        MainContentArea.Content = iwsMenuView;
                        iwsMenuView.ApplyOSFilter(SelectedOS);
                    }
                    else
                    {
                        var iwsView = new IWSView();
                        MainContentArea.Content = iwsView;
                        iwsView.ApplyOSFilter(SelectedOS);
                    }
                    TxtInfo.Text = "Modul: IWS - Telepítés és Biztonság";
                    break;

                case "BtnNet":
                    TxtInfo.Text = "Modul: Halozat (Network-Tools)";
                    if (!ModuleRunner.ModuleInstalled("Network-Tools"))
                    {
                        MainContentArea.Content = null;
                        LogToConsole("[Network-Tools] Nincs telepitve - nyisd meg a MOD nezetet, vagy varj a telepites vegere.");
                    }
                    else if (ModuleMenuCatalog.HasMenu("Network-Tools"))
                    {
                        // Ha kesobb keszul rts-menu.json a Network-Tools-hoz, automatikusan
                        // a beepitett, gomb-szintu menut hasznaljuk a kulon ablak helyett.
                        var netMenuView = new Views.ModuleMenuView("Network-Tools");
                        MainContentArea.Content = netMenuView;
                        netMenuView.ApplyOSFilter(SelectedOS);
                    }
                    else
                    {
                        // Egyelore nincs rts-menu.json a Network-Tools-hoz (nem volt resze
                        // ennek a kornek) - a teljes sajat menujet nyitjuk meg, vegso esetkent.
                        MainContentArea.Content = null;
                        var netModule = ModuleCatalog.Load().Find(m => m.Name == "Network-Tools");
                        string netEntry = netModule?.EntryPoint ?? "win\\Launcher.ps1";
                        LogToConsole("[Network-Tools] Meg nincs gomb-szintu menuje ebben a korben - a modul sajat teljes menuje nyilik meg, kulon ablakban.");
                        var netResult = ModuleRunner.Run("Network-Tools", netEntry);
                        LogToConsole(netResult.Message);
                    }
                    break;

                case "BtnModules":
                    var modulesView = new ModulesView();
                    MainContentArea.Content = modulesView;
                    TxtInfo.Text = "Modul: Osszes modul";
                    break;

                case "BtnHome":
                    MainContentArea.Content = null;
                    TxtInfo.Text = "Rendszer készenlétben...";
                    break;

                default:
                    if (btn.Name.StartsWith("BtnWin") || btn.Name == "BtnXP")
                    {
                        HighlightOSButton(btn);
                        LogToConsole($"OS váltva: {SelectedOS}");
                    }
                    break;
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            // theme váltás (marad a korábbi kódod)
            isDark = !isDark;
            // ... a ResourceDictionary csere maradjon a te verziód szerint
            HighlightOSButton(activeOSButton!);
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bezárja az RTS alkalmazást?", "Nexus RTS", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }

        private void BtnGH_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/LordAthis/RTS",
                    UseShellExecute = true
                });
                LogToConsole("GitHub repó megnyitása...");
            }
            catch (Exception ex)
            {
                LogToConsole("Hiba a böngésző indításakor: " + ex.Message);
            }
        }
    }
}