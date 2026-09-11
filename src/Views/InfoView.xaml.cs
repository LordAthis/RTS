// Verzio: v0.4.0 - 2026-09-11
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Models;
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

            AddRow("RTS verzio", $"{RtsVersion.Version} ({RtsVersion.BuildDate})");
            AddRow("Operacios rendszer", Environment.OSVersion.VersionString);
            AddRow("Rendszergazda mod", isAdmin ? "Igen" : "Nem (nem varhato - lasd app.manifest)");
            AddRow("Adatmappa", root);
            AddRow("modules.json allapot", hasModulesJson ? "Megtalalva" : "HIANYZIK - telepites szukseges");
            if (hasModulesJson)
                AddRow("Modulok", $"{installedCount} / {enabledCount} engedelyezett modul van letoltve");

            if (_lastUpdateCheck != null)
            {
                int updateCount = _lastUpdateCheck.Count(u => u.HasUpdate);
                int errorCount = _lastUpdateCheck.Count(u => u.Error != null);
                string summary = updateCount > 0
                    ? $"{updateCount} modulhoz erheto el frissebb valtozat"
                    : "Minden ellenorzott modul naprakesz";
                if (errorCount > 0) summary += $" ({errorCount} modulnal nem sikerult ellenorizni)";
                AddRow("Frissites-ellenorzes", summary);

                foreach (var u in _lastUpdateCheck.Where(x => x.HasUpdate))
                {
                    string dateStr = u.RemoteCommitDate?.ToString("yyyy-MM-dd") ?? "ismeretlen datum";
                    AddRow($"  -> {u.Name}", $"uj commit elerheto ({dateStr}) - kattints \"Modulok ujratelepitese\"-re a frissiteshez");
                }
            }
        }

        // Konnyu-sulyu, csak-olvas ellenorzes: NEM tolt le semmit, csak
        // megnezi a GitHub-on levo legfrissebb commit-okat es osszeveti a
        // helyben telepitettekkel. Kulon gombbol indithato, hogy a
        // felhasznalo a tenyleges (idoigenyesebb) ujratelepites elott lassa,
        // egyaltalan van-e mit frissiteni.
        private List<ModuleUpdateStatus>? _lastUpdateCheck;

        private void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            var mainWin = (MainWindow)Application.Current.MainWindow;
            string root = ModuleRunner.FindRtsRoot();

            if (!File.Exists(Path.Combine(root, "modules.json")))
            {
                mainWin.LogToConsole("Nincs meg modules.json - eloszor telepitsd az RTS-t.");
                return;
            }

            mainWin.LogToConsole("Frissitesek keresese a telepitett modulokhoz...");
            var modules = ModuleCatalog.Load();
            Task.Run(() =>
            {
                var result = RtsInstaller.CheckForUpdates(root, modules);
                Dispatcher.Invoke(() =>
                {
                    _lastUpdateCheck = result;
                    LoadInfo();
                    int updateCount = result.Count(u => u.HasUpdate);
                    mainWin.LogToConsole(updateCount > 0
                        ? $"Frissites-ellenorzes kesz: {updateCount} modulhoz van ujabb valtozat."
                        : "Frissites-ellenorzes kesz: minden telepitett modul naprakesz.");
                });
            });
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
