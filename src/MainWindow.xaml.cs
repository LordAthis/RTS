using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
            LogToConsole("RTS Keretrendszer elindítva. Rendszergazdai jogok ellenőrizve.");
        }

        // 1. ABLAK MOZGATÁSA (Mivel nincs gyári címsor)
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // 2. OS FELISMERÉSE ÉS JELZÉSE
        private void DetectCurrentOS()
        {
            var os = Environment.OSVersion;
            int major = os.Version.Major;
            int minor = os.Version.Minor;
            int build = os.Version.Build;

            // Logikailag kiválasztjuk, melyik gombot gyújtsuk meg
            // Megjegyzés: A XAML-ben a gomboknak x:Name="BtnWin10" stb. nevet kell adni
            if (major == 5) HighlightOSButton(BtnXP);       // XP
            else if (major == 6 && minor == 1) HighlightOSButton(BtnWin7);  // Win 7
            else if (major == 6 && (minor == 2 || minor == 3)) HighlightOSButton(BtnWin8); // Win 8
            else if (major == 10 && build < 22000) HighlightOSButton(BtnWin10); // Win 10
            else if (major == 10 && build >= 22000) HighlightOSButton(BtnWin11); // Win 11
            
            LogToConsole($"Észlelt rendszer: Windows {major}.{minor} (Build: {build})");
        }

        private void HighlightOSButton(Button target)
        {
            if (target == null) return;
            
            // Ha volt korábban aktív, azt visszaállítjuk alapra
            if (activeOSButton != null) 
                activeOSButton.Background = Brushes.Transparent;

            // Az aktív gomb kap egy erős neon zöld hátteret (vagy stílust)
            target.Background = (SolidColorBrush)Application.Current.Resources["AccentNeon"];
            target.Foreground = Brushes.Black; // Hogy olvasható legyen a világoson
            activeOSButton = target;
        }

        // 3. LOG ABLAK KEZELÉSE
        public void LogToConsole(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.Text += $"[{time}] {message}\n";
            
            // Automatikus görgetés az aljára
            var scroll = (ScrollViewer)TxtLog.Parent;
            scroll.ScrollToBottom();
        }

        // 4. MODULOK BETÖLTÉSE (F1-F4 és OS Gombok)
        private void Module_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            string moduleName = btn.Content.ToString() ?? "";
            TxtInfo.Text = $"Aktív modul: {moduleName} | Várakozás parancsra...";
            
            // Itt villan meg a kék neon kattintáskor (a XAML trigger intézi)
            LogToConsole($"{moduleName} modul betöltve.");

            // Később itt hívjuk be a konkrét repókat (IWS, ATI, stb.)
        }

        // 5. TÉMA VÁLTÓ
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ResourceDictionary newTheme = new ResourceDictionary();
            if (isDark)
                newTheme.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
            else
                newTheme.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(newTheme);
            isDark = !isDark;
            
            // Frissítjük az aktív OS gomb színét a váltás után
            if (activeOSButton != null) HighlightOSButton(activeOSButton);
            
            LogToConsole($"Téma váltva: {(isDark ? "Sötét" : "Világos")}");
        }

        // 6. KILÉPÉS
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Biztosan ki akarsz lépni?", "RTS Kilépés", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
