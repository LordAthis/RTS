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
            // Az indítás után azonnal detektáljuk az oprendszert
            DetectCurrentOS();
            LogToConsole("RTS Keretrendszer inicializálva. Készen áll a használatra.");
        }

        // 1. ABLAK MOZGATÁSA (Mivel nincs gyári címsor)
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // 2. OS FELISMERÉSE ÉS VIZUÁLIS JELZÉSE
        private void DetectCurrentOS()
        {
            var os = Environment.OSVersion;
            int major = os.Version.Major;
            int minor = os.Version.Minor;
            int build = os.Version.Build;

            // Itt használjuk a XAML-ben megadott x:Name azonosítókat
            if (major == 5) HighlightOSButton(BtnXP); 
            else if (major == 6 && minor == 1) HighlightOSButton(BtnWin7);
            else if (major == 6 && (minor == 2 || minor == 3)) HighlightOSButton(BtnWin8);
            else if (major == 10 && build < 22000) HighlightOSButton(BtnWin10);
            else if (major == 10 && build >= 22000) HighlightOSButton(BtnWin11);
            
            LogToConsole($"Rendszerazonosítás sikeres: Windows {major}.{minor} (Build: {build})");
        }

        private void HighlightOSButton(Button target)
        {
            if (target == null) return;
            
            // Ha volt korábbi kijelölés, azt alaphelyzetbe hozzuk
            if (activeOSButton != null) 
            {
                activeOSButton.Background = Brushes.Transparent;
                activeOSButton.Foreground = (SolidColorBrush)Application.Current.Resources["TextBrush"];
            }

            // Az aktuális rendszer gombja megkapja a neon zöld "világítást"
            target.Background = (SolidColorBrush)Application.Current.Resources["AccentNeon"];
            target.Foreground = Brushes.Black; // Sötét szöveg a világító háttéren a kontraszt miatt
            activeOSButton = target;
        }

        // 3. LOG KEZELÉSE (B3 TERÜLET)
        public void LogToConsole(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.Text += $"[{time}] {message}\n";
            
            // Automatikus görgetés az aljára a LogScroller segítségével
            LogScroller.ScrollToBottom();
        }

        // 4. MODULOK ÉS GOMBOK KEZELÉSE (Click események)
        private void Module_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            string moduleName = btn.Content.ToString() ?? "Ismeretlen";
            
            // Info sáv (A1) frissítése
            TxtInfo.Text = $"Kiválasztott modul: {moduleName} | Műveletre vár...";
            
            LogToConsole($"{moduleName} kiválasztva.");

            // Ha nem a Home gombot nyomtuk meg, elindíthatunk egy kis animációt a kereten
            if (btn.Name != "BtnHome")
            {
                Storyboard? sb = (Storyboard)this.FindResource("WorkingAnimation");
                sb?.Begin();
            }
        }

        // 5. TÉMA VÁLTÁSA (DARK / LIGHT)
        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ResourceDictionary newTheme = new ResourceDictionary();
            try 
            {
                if (isDark)
                    newTheme.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                else
                    newTheme.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);

                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(newTheme);
                isDark = !isDark;

                // Frissítjük a kijelölt gombot is, hogy az új színekkel is jól mutasson
                if (activeOSButton != null) HighlightOSButton(activeOSButton);
                
                LogToConsole($"Téma váltva: {(isDark ? "Sötét" : "Világos")}");
            }
            catch (Exception ex)
            {
                LogToConsole($"Hiba a témaváltáskor: {ex.Message}");
            }
        }

        // 6. KILÉPÉS
        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            // Egy gyors megerősítés, nehogy véletlenül bezárjuk munka közben
            var result = MessageBox.Show("Biztosan be akarod zárni az RTS szervizalkalmazást?", 
                                       "Kilépés megerősítése", 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
