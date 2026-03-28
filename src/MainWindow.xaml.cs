using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using RTS.Views; // Ez fontos a nézetek betöltéséhez

namespace RTS
{
    public partial class MainWindow : Window
    {
        private bool isDark = true;
        private Button? activeOSButton = null;

        public MainWindow()
        {
            InitializeComponent();
            DetectCurrentOS();
            LogToConsole("RTS Keretrendszer inicializálva. Készen áll a használatra.");
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void DetectCurrentOS()
        {
            var os = Environment.OSVersion;
            int major = os.Version.Major;
            int minor = os.Version.Minor;
            int build = os.Version.Build;

            if (major == 5) HighlightOSButton(BtnXP); 
            else if (major == 6 && minor == 1) HighlightOSButton(BtnWin7);
            else if (major == 6 && (minor == 2 || minor == 3)) HighlightOSButton(BtnWin8);
            else if (major == 10 && build < 22000) HighlightOSButton(BtnWin10);
            else if (major == 10 && build >= 22000) HighlightOSButton(BtnWin11);
            
            LogToConsole($"Rendszerazonosítás: Windows {major}.{minor} (Build: {build})");
        }

        private void HighlightOSButton(Button target)
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
        }

        public void LogToConsole(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.Text += $"[{time}] {message}\n";
            LogScroller.ScrollToBottom();
        }

        // --- MODULVÁLTÓ LOGIKA (MINDEN GOMBHOZ) ---
        private void Module_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            // Alapvető animáció indítása
            if (btn.Name != "BtnHome")
            {
                Storyboard? sb = (Storyboard)this.FindResource("WorkingAnimation");
                sb?.Begin();
            }

            // Gombnév alapján betöltjük a megfelelő UserControl-t az A2 területre
            switch (btn.Name)
            {
                case "BtnIWS":
                    MainContentArea.Content = new IWSView();
                    TxtInfo.Text = "Modul: IWS - Telepítés és Biztonsági keményítés.";
                    LogToConsole("IWS Modul betöltve.");
                    break;

                case "BtnNet":
                    // Ha még nincs kész a nézet, jelezzük
                    TxtInfo.Text = "Modul: Hálózat és Hardening.";
                    LogToConsole("NET Modul betöltése... (Fejlesztés alatt)");
                    // MainContentArea.Content = new NetView(); // Majd ha kész a fájl
                    break;

                case "BtnATI":
                    TxtInfo.Text = "Modul: ATI - Automatikus Driver telepítés.";
                    LogToConsole("ATI Modul betöltése... (Hamarosan)");
                    break;

                case "BtnRTS":
                    TxtInfo.Text = "Modul: RTS - Remote Technical Support eszközök.";
                    LogToConsole("RTS Eszközök betöltve.");
                    break;

                case "BtnHome":
                    MainContentArea.Content = null;
                    TxtInfo.Text = "Rendszer készenlétben...";
                    LogToConsole("Visszatérés a kezdőképernyőre.");
                    break;

                default:
                    LogToConsole($"{btn.Content} OS specifikus mód kiválasztva.");
                    break;
            }
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ResourceDictionary newTheme = new ResourceDictionary();
            try {
                newTheme.Source = isDark ? new Uri("Themes/LightTheme.xaml", UriKind.Relative) : new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(newTheme);
                isDark = !isDark;
                if (activeOSButton != null) HighlightOSButton(activeOSButton);
                LogToConsole($"Téma: {(isDark ? "Sötét" : "Világos")}");
            } catch (Exception ex) { LogToConsole($"Hiba: {ex.Message}"); }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bezárja az RTS alkalmazást?", "Kilépés", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }
    }
}
