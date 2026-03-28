using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RTS.Views;

namespace RTS
{
    public partial class MainWindow : Window
    {
        private bool isDark = true;
        private Button? activeOSButton = null;
        
        // Globálisan elérhető állapot az OS szűréshez
        public string SelectedOS { get; private set; } = "10"; // Alapértelmezett

        public MainWindow()
        {
            InitializeComponent();
            // 1. Detektálás indításkor
            DetectCurrentOS();
            LogToConsole("NEXUS RTS Rendszer betöltve. Keretrendszer készen áll.");
        }

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

            // Pontos azonosítás és a gomb kigyújtása
            if (major == 5) HighlightOSButton(BtnXP); 
            else if (major == 6 && minor == 1) HighlightOSButton(BtnWin7);
            else if (major == 6 && (minor == 2 || minor == 3)) HighlightOSButton(BtnWin8);
            else if (major == 10 && build < 22000) HighlightOSButton(BtnWin10);
            else if (major == 10 && build >= 22000) HighlightOSButton(BtnWin11);
            
            LogToConsole($"Észlelt OS: Windows {SelectedOS} (Build: {build})");
        }

        public void HighlightOSButton(Button target)
        {
            if (target == null) return;
            
            // Régi gomb leoltása
            if (activeOSButton != null) 
            {
                activeOSButton.Background = Brushes.Transparent;
                activeOSButton.Foreground = (SolidColorBrush)Application.Current.Resources["TextBrush"];
            }

            // Új gomb kigyújtása
            target.Background = (SolidColorBrush)Application.Current.Resources["AccentNeon"];
            target.Foreground = Brushes.Black;
            activeOSButton = target;
            SelectedOS = target.Content.ToString()!;

            // Frissítjük a jelenleg látható modult, ha van
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

            // Neon animáció indítása (dolgozik a gép)
            Storyboard? sb = (Storyboard)this.FindResource("WorkingAnimation");
            sb?.Begin();

            switch (btn.Name)
            {
                case "BtnIWS":
                    var iwsView = new IWSView();
                    MainContentArea.Content = iwsView;
                    iwsView.ApplyOSFilter(SelectedOS); // Azonnali szűrés betöltéskor
                    TxtInfo.Text = "Modul: IWS - Telepítés és Biztonság";
                    break;
                case "BtnNet":
                    TxtInfo.Text = "Modul: Hálózat és Hardening (Hamarosan)";
                    LogToConsole("Hálózati modul előkészítése...");
                    break;
                case "BtnHome":
                    MainContentArea.Content = null;
                    TxtInfo.Text = "Rendszer készenlétben...";
                    break;
                default:
                    // Ha OS választót nyomtunk (XP, 7, stb.)
                    if (btn.Name.StartsWith("BtnWin") || btn.Name == "BtnXP") {
                        HighlightOSButton(btn);
                        LogToConsole($"OS Nézet váltva: {SelectedOS}");
                    }
                    break;
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ResourceDictionary theme = new ResourceDictionary();
            theme.Source = new Uri(isDark ? "Themes/LightTheme.xaml" : "Themes/DarkTheme.xaml", UriKind.Relative);
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(theme);
            isDark = !isDark;
            HighlightOSButton(activeOSButton!); // Színfrissítés
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bezárja az RTS alkalmazást?", "Nexus RTS", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }
    }
}
