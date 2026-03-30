using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
                case "BtnRTS":   // most F1
                    LogToConsole("F1 gomb lenyomva – funkció később implementálva");
                    break;

                case "BtnIWS":
                    var iwsView = new IWSView();
                    MainContentArea.Content = iwsView;
                    iwsView.ApplyOSFilter(SelectedOS);
                    TxtInfo.Text = "Modul: IWS - Telepítés és Biztonság";
                    break;

                case "BtnNet":
                    TxtInfo.Text = "Modul: Hálózat (hamarosan)";
                    LogToConsole("Hálózati modul előkészítése...");
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