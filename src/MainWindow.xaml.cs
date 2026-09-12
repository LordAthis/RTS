// Verzio: v0.4.2 - 2026-09-12 (lasd RTS.Models.RtsVersion a tenyleges,
// kozponti verzioszamert - ez a komment csak emberi olvasasra/kovetesre
// szolgal, a tenyleges frissites-ellenorzes NEM ebbol dolgozik)
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
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
            ShowHomeStatus();
            EnsureRtsInstalled();

            // v0.4.2 - LOG feldolgozo egyseg ELOKESZITESE (vazlat, lasd
            // Services/LogProcessor.cs): minden inditaskor letrehozza/
            // ellenorzi a ket seged-fajlt, es ha van bennuk barmi, kiirja.
            try { LogProcessor.RunStartupCheck(ModuleRunner.FindRtsRoot(), LogToConsole); }
            catch (Exception ex) { LogToConsole("[LOG feldolgozo] Hiba az inditasi ellenorzeskor: " + ex.Message); }
        }

        // A "🏠 Home" gomb altal (es inditaskor) mutatott allapot-osszefoglalo:
        // verzio + hany modul van telepitve/engedelyezve. A frissites-
        // ellenorzest (GitHub-hivasokat igenyel) SZANDEKOSAN nem futtatjuk
        // itt automatikusan minden inditaskor/kattintasra - az InfoView
        // (F1) sajat "Frissitesek keresese" gombja vegzi, hogy ne lassitsa
        // az inditast es ne fogyassza feleslegesen a GitHub API kvotat.
        private void ShowHomeStatus()
        {
            string root = ModuleRunner.FindRtsRoot();
            string statusLine = $"RTS v{RTS.Models.RtsVersion.Version} ({RTS.Models.RtsVersion.BuildDate})";

            if (System.IO.File.Exists(System.IO.Path.Combine(root, "modules.json")))
            {
                var modules = ModuleCatalog.Load();
                int enabledCount = modules.Count(m => m.Enabled);
                int installedCount = modules.Count(m => m.Enabled && ModuleRunner.ModuleInstalled(m.Name));
                statusLine += $" | {installedCount}/{enabledCount} modul telepitve | Frissites-ellenorzeshez: F1 -> \"Frissitesek keresese\"";
            }
            else
            {
                statusLine += " | Meg nincs telepitve egy modul sem - lasd a konzolt.";
            }

            TxtInfo.Text = statusLine;
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

        // Csak a felso info-savot frissiti (pl. egy menu-tetel .md leirasat
        // mutatja egy kattintasra), a fo tartalom (MainContentArea) valtozatlan
        // marad - lasd ModuleMenuView egykattintas=info logikaja.
        public void SetInfoText(string text)
        {
            TxtInfo.Text = text;
        }

        public void LogToConsole(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{time}] {message}";
            TxtLog.Text += line + "\n";
            LogScroller.ScrollToBottom();
            AppendToRollingLogFile(line);
        }

        // ─────────────────────────────────────────────
        //  HATTER-LOG: minden LogToConsole-hivas emellett egy napi
        //  gorgetheto fajlba is bekerul (<gyoker>\LOG\rts-YYYY-MM-DD.log),
        //  hogy utolag is lathato legyen, mi tortent a hatterben - akkor
        //  is, ha a felhasznalo nem menti el kezzel a LOG gombbal.
        //  Hibat sose dob tovabb - ez csak kiegeszito naplozas.
        // ─────────────────────────────────────────────
        private void AppendToRollingLogFile(string line)
        {
            try
            {
                string root = ModuleRunner.FindRtsRoot();
                string logDir = Path.Combine(root, "LOG");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir, $"rts-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
            catch
            {
                // szandekosan elnyelve - a hatter-naplozas hibaja ne akassza meg az UI-t
            }
        }

        private void BtnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string root = ModuleRunner.FindRtsRoot();
                string logDir = Path.Combine(root, "LOG");
                Directory.CreateDirectory(logDir);

                var dlg = new SaveFileDialog
                {
                    InitialDirectory = logDir,
                    FileName = $"rts-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                    Filter = "Szoveges fajl (*.txt)|*.txt|Minden fajl (*.*)|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, TxtLog.Text);
                    LogToConsole($"Log elmentve: {dlg.FileName}");
                }
            }
            catch (Exception ex)
            {
                LogToConsole("Hiba a log mentesekor: " + ex.Message);
            }
        }

        private void BtnWeb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://lordathis.blogspot.com",
                    UseShellExecute = true
                });
                LogToConsole("Blog megnyitasa...");
            }
            catch (Exception ex)
            {
                LogToConsole("Hiba a bongeszo inditasakor: " + ex.Message);
            }
        }

        // Kozos logika a fejlec-gyorsgombokhoz (IWS, NET, WRT, STP, ...):
        // ha van rts-menu.json a modulhoz, azt nyitja meg a sajat feluleten
        // belul; ha nincs telepitve, egyertelmu uzenetet ir; ha telepitve
        // van de meg nincs gomb-szintu menuje, vegso esetkent a modul sajat
        // teljes belepesi pontjat inditja (kulon ablakban).
        private void OpenModuleQuick(string moduleName, string infoLabel)
        {
            TxtInfo.Text = infoLabel;

            if (ModuleMenuCatalog.HasMenu(moduleName))
            {
                var menuView = new Views.ModuleMenuView(moduleName);
                MainContentArea.Content = menuView;
                menuView.ApplyOSFilter(SelectedOS);
                return;
            }

            if (!ModuleRunner.ModuleInstalled(moduleName))
            {
                MainContentArea.Content = null;
                LogToConsole($"[{moduleName}] Nincs telepitve - nyisd meg a MOD nezetet, vagy varj a telepites vegere.");
                return;
            }

            MainContentArea.Content = null;
            var module = ModuleCatalog.Load().Find(m => m.Name == moduleName);
            string entry = module?.EntryPoint ?? "";
            if (string.IsNullOrWhiteSpace(entry))
            {
                LogToConsole($"[{moduleName}] Nincs beallitva belepesi pont a modules.json-ban.");
                return;
            }
            LogToConsole($"[{moduleName}] Meg nincs gomb-szintu menuje ebben a korben - a modul sajat teljes menuje nyilik meg, kulon ablakban.");
            var result = ModuleRunner.Run(moduleName, entry);
            LogToConsole(result.Message);
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
                    // Ha meg nincs rts-menu.json (nem varhato, mar van), a regi,
                    // kezzel irt IWSView-ra esunk vissza kompatibilitasi okbol.
                    if (!ModuleMenuCatalog.HasMenu("IWS") && ModuleRunner.ModuleInstalled("IWS"))
                    {
                        var iwsView = new IWSView();
                        MainContentArea.Content = iwsView;
                        iwsView.ApplyOSFilter(SelectedOS);
                        TxtInfo.Text = "Modul: IWS - Telepítés és Biztonság";
                    }
                    else
                    {
                        OpenModuleQuick("IWS", "Modul: IWS - Telepítés és Biztonság");
                    }
                    break;

                case "BtnNet":
                    OpenModuleQuick("Network-Tools", "Modul: Halozat (Network-Tools)");
                    break;

                case "BtnWRT":
                    OpenModuleQuick("WinRegTools", "Modul: WinRegTools (WRT)");
                    break;

                case "BtnSTP":
                    OpenModuleQuick("SetUpER", "Modul: SetUpER (STP)");
                    break;

                case "BtnModules":
                    var modulesView = new ModulesView();
                    MainContentArea.Content = modulesView;
                    TxtInfo.Text = "Modul: Osszes modul";
                    break;

                case "BtnHome":
                    MainContentArea.Content = null;
                    ShowHomeStatus();
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

        // Verzio v0.4.2 - 2026-09-12: a korabban meg linkeletlen LinkedIn
        // gomb vegre be van kotve.
        private void BtnLn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.linkedin.com/in/lordathis/",
                    UseShellExecute = true
                });
                LogToConsole("LinkedIn megnyitása...");
            }
            catch (Exception ex)
            {
                LogToConsole("Hiba a böngésző indításakor: " + ex.Message);
            }
        }

        // Verzio v0.4.2 - 2026-09-12: uj gomb - a LOG mappa elkuldese
        // DiagMailer-rel. Lasd: Services/DiagMailerLauncher.cs a reszletekert
        // (miert kulon "SendReport.ps1" hivas, es nem a Launcher.ps1 menuje).
        private void BtnMail_Click(object sender, RoutedEventArgs e)
        {
            var result = Services.DiagMailerLauncher.SendLogFolder(LogToConsole);
            LogToConsole(result.Message);
        }
    }
}