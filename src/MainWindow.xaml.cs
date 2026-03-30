using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RTS.Views;
using System.Diagnostics;

namespace RTS
{
    public partial class MainWindow : Window
    {
        private bool isDark = true;
        private Button? activeOSButton = null;
        
        // Globálisan elérhető állapot az OS szűréshez
        public string SelectedOS { get; private set; } = "10"; 

        public MainWindow()
        {
            InitializeComponent();
            // Detektálás indításkor
            DetectCurrentOS();
            LogToConsole("NEXUS RTS Rendszer betöltve. Keretrendszer készen áll.");
        }

        // Ablak mozgatása a fejlécnél fogva
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void DetectCurrentOS()
        {
            var os = Environment.OSVersion;
            int major = os.Version.Major;
            int minor = os.Version.Minor;
            int build = os.Version.Build;

            // Windows verzió pontos azonosítása
            if (major == 5) HighlightOSButton(BtnXP); 
            else if (major == 6 && minor == 1) HighlightOSButton(BtnWin11); // Itt a gombok neveit a XAML-hez igazítottuk
            else if (major == 10 && build < 22000) HighlightOSButton(BtnWin11);
            else if (major == 10 && build >= 22000) HighlightOSButton(BtnWin11);
            
            LogToConsole($"Észlelt OS Build: {build}");
        }

        public void HighlightOSButton(Button target)
        {
            if (target == null) return;
            
            // Régi gomb alaphelyzetbe állítása
            if (activeOSButton != null) 
            {
                activeOSButton.Background = Brushes.Transparent;
                activeOSButton.Foreground = (SolidColorBrush)Application.Current.Resources["TextBrush"];
            }

            // Új gomb kigyújtása (itt már biztonságos az átalakítás az új Themes.xaml-el)
            target.Background = (SolidColorBrush)Application.Current.Resources["AccentNeon"];
            target.Foreground = Brushes.Black;
            
            activeOSButton = target;
            SelectedOS = target.Content.ToString()!;

            // Frissítjük a modult, ha épp nyitva van az IWS
            if (MainContentArea.Content is IWSView iws) iws.ApplyOSFilter(SelectedOS);
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

            // Az A1 terület neonzöld lüktetésének indítása (visszajelzés, hogy dolgozik)
            Storyboard? sb = (Storyboard)this.FindResource("A1_WorkAnim");
            sb?.Begin();

            switch (btn.Name)
            {
                case "BtnIWS":
                    var iwsView = new IWSView();
                    MainContentArea.Content = iwsView;
                    iwsView.ApplyOSFilter(SelectedOS);
                    TxtInfo.Text = "Modul: IWS - Telepítés és Biztonság";
                    LogToConsole("IWS Modul betöltve.");
                    break;

                case "BtnRTS":
                    MainContentArea.Content = null; // Ide jöhet majd az RTS nézete
                    TxtInfo.Text = "Modul: RTS Core";
                    LogToConsole("RTS Core aktiválva.");
                    break;

                case "BtnHome":
                    MainContentArea.Content = null;
                    TxtInfo.Text = "Rendszer készenlétben...";
                    sb?.Stop(); // Home-nál megállítjuk az animációt
                    break;

                default:
                    // OS választó gombok kezelése (XP, 11, stb.)
                    if (btn.Name.Contains("Win") || btn.Name == "BtnXP") {
                        HighlightOSButton(btn);
                        LogToConsole($"OS fókusz váltva: Windows {SelectedOS}");
                    }
                    break;
            }
        }

        // GitHub link megnyitása alapértelmezett böngészőben
        private void BtnGH_Click(object sender, RoutedEventArgs e)
        {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = "https://github.com/LordAthis/RTS",
                    UseShellExecute = true
                });
                LogToConsole("GitHub repó megnyitása...");
            } catch (Exception ex) {
                LogToConsole("Hiba a böngésző indításakor: " + ex.Message);
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ResourceDictionary theme = new ResourceDictionary();
            theme.Source = new Uri(isDark ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml", UriKind.Relative);
            
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(theme);
            
            isDark = !isDark;
            if (activeOSButton != null) HighlightOSButton(activeOSButton);
            LogToConsole(isDark ? "Sötét mód aktív." : "Világos mód aktív.");
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
