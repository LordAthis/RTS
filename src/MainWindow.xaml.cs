using System.Windows;
using System;

namespace RTS
{
    public partial class MainWindow : Window
    {
        private bool isDark = true;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Ablak mozgatása (Mivel nincs címsor)
        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            // Egyszerű téma váltó logika
            ResourceDictionary newTheme = new ResourceDictionary();
            if (isDark)
                newTheme.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
            else
                newTheme.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(newTheme);
            isDark = !isDark;
        }
    }
}
// Példa egy funkció hívására
private void RunScript(string scriptPath) {
    // Animáció indítása
    Storyboard sb = (Storyboard)this.FindResource("WorkingAnimation");
    sb.Begin();

    TxtLog.Text += $"\n> Futtatás: {scriptPath}...";
    TxtInfo.Text = $"Folyamatban: {scriptPath} végrehajtása. Kérlek várj...";

    // Itt hívjuk meg majd a tényleges PowerShell/Batch fájlt
    
    // Ha végzett (ez most csak szimuláció):
    // sb.Stop();
}
