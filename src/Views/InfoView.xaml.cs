using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Services;

namespace RTS.Views
{
    // "F1" nezet: alap rendszer-/telepitesinfo, plusz nehany gyors muvelet.
    public partial class InfoView : UserControl
    {
        public InfoView()
        {
            InitializeComponent();
            LoadInfo();
        }

        private void LoadInfo()
        {
            InfoPanel.Children.Clear();

            string root = ModuleRunner.FindRtsRoot();
            bool hasModulesJson = File.Exists(Path.Combine(root, "modules.json"));
            bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            int enabledCount = 0, installedCount = 0;
            if (hasModulesJson)
            {
                var modules = ModuleCatalog.Load();
                enabledCount = modules.Count(m => m.Enabled);
                installedCount = modules.Count(m => m.Enabled && ModuleRunner.ModuleInstalled(m.Name));
            }

            AddRow("RTS verzio", "0.2.0");
            AddRow("Operacios rendszer", Environment.OSVersion.VersionString);
            AddRow("Rendszergazda mod", isAdmin ? "Igen" : "Nem (nem varhato - lasd app.manifest)");
            AddRow("Adatmappa", root);
            AddRow("modules.json allapot", hasModulesJson ? "Megtalalva" : "HIANYZIK - telepites szukseges");
            if (hasModulesJson)
                AddRow("Modulok", $"{installedCount} / {enabledCount} engedelyezett modul van letoltve");
        }

        private void AddRow(string label, string value)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new TextBlock
            {
                Text = label + ":",
                Foreground = Brushes.Gray,
                Width = 190,
                FontSize = 12
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            });
            InfoPanel.Children.Add(row);
        }

        private void BtnOpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            string root = ModuleRunner.FindRtsRoot();
            try
            {
                Directory.CreateDirectory(root);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{root}\"") { UseShellExecute = true });
                mainWin.LogToConsole($"Adatmappa megnyitva: {root}");
            }
            catch (Exception ex)
            {
                mainWin.LogToConsole("Hiba az adatmappa megnyitasakor: " + ex.Message);
            }
        }

        private void BtnReinstall_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            string root = ModuleRunner.FindRtsRoot();

            if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "modules.json")))
            {
                mainWin.LogToConsole("Nincs meg ervenyes adatmappa - eloszor inditsd ujra az RTS-t es valassz telepitesi mappat.");
                return;
            }

            mainWin.LogToConsole($"Modulok ujratelepitese/ellenorzese inditva ebbe a mappaba: {root}");
            Task.Run(() =>
            {
                RtsInstaller.RunFirstTimeSetup(root, msg => Dispatcher.Invoke(() => mainWin.LogToConsole(msg)));
                Dispatcher.Invoke(() =>
                {
                    ModuleRunner.ResetRootCache();
                    LoadInfo();
                    mainWin.LogToConsole("Ujratelepites/ellenorzes kesz.");
                });
            });
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadInfo();
    }
}
