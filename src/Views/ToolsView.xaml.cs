// Verzio: v0.6.1 - 2026-09-15
// DRAFT - lasd ToolsView.xaml fejlec-megjegyzeset. Ez a nezet mar
// tenylegesen meghivja a HardwareQueryService-t/ToolAcquisition-t/
// RustDeskLauncher-t (nem csak navigacios belepesi pont, mint a korabbi
// stub).
//
// Verzio v0.6.1 - uj: RustDesk sor (SetUpER-en keresztuli telepites) +
// "automatikus telepites" jelolonegyzet+Alkalmaz (ToolsSettingsService).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Models;
using RTS.Services;

namespace RTS.Views
{
    public partial class ToolsView : UserControl
    {
        private bool _autoInstallRunning;

        public ToolsView()
        {
            InitializeComponent();
            LoadFromCache();
            ChkAutoInstall.IsChecked = ToolsSettingsService.Load().AutoInstall;
            BuildToolStatusList();
            _ = AutoAcquireIfEnabledAsync();
        }

        private void LoadFromCache()
        {
            var cached = HardwareQueryService.LoadCached();
            Render(cached);
        }

        private void Render(HardwareInfo? info)
        {
            if (info == null)
            {
                TxtLastQueried.Text = "Meg nem tortent lekerdezes - kattints az \"Informaciok frissitese\" gombra.";
                TxtSummary.Text = "Nincs adat.";
                return;
            }

            TxtLastQueried.Text = info.QueriedAtUtc.HasValue
                ? $"Utolso lekerdezes: {info.QueriedAtUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "Meg nem tortent lekerdezes.";

            string cpu = string.IsNullOrWhiteSpace(info.Cpu.Summary) ? "(ismeretlen)" : info.Cpu.Summary;
            string disk = info.Disk.HealthPercent.HasValue ? $"{info.Disk.HealthPercent}%" : "(ismeretlen)";
            TxtSummary.Text =
                $"Gep: {info.Machine.MachineName}\n" +
                $"OS: {info.Machine.OsCaption} ({info.Machine.OsVersion})\n" +
                $"CPU: {cpu}\n" +
                $"Lemez-egeszseg: {disk}";

            TxtCpuRaw.Text = info.Cpu.RawText.Length > 0 ? info.Cpu.RawText : (info.Cpu.Error ?? "Nincs adat.");
            TxtGpuRaw.Text = info.Gpu.RawText.Length > 0 ? info.Gpu.RawText : (info.Gpu.Error ?? "Nincs adat.");
            TxtDiskRaw.Text = info.Disk.RawText.Length > 0 ? info.Disk.RawText : (info.Disk.Error ?? "Nincs adat.");
            TxtDxDiagRaw.Text = info.DxDiag.RawText.Length > 0 ? info.DxDiag.RawText : (info.DxDiag.Error ?? "Nincs adat.");
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            BtnRefresh.IsEnabled = false;
            TxtLastQueried.Text = "Lekerdezes folyamatban - ez eltarthat fel percig (CPU-Z, GPU-Z, H.D. Sentinel, DXDIAG egymas utan)...";
            try
            {
                var info = await HardwareQueryService.RefreshAsync(Log);
                Render(info);
                BuildToolStatusList();
            }
            catch (Exception ex)
            {
                Log("Hiba a lekerdezes soran: " + ex.Message);
            }
            finally
            {
                BtnRefresh.IsEnabled = true;
            }
        }

        private void BtnApplyAutoInstall_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkAutoInstall.IsChecked == true;
            ToolsSettingsService.Save(enabled);
            Log(enabled
                ? "[Eszkozok] Automatikus telepites BEKAPCSOLVA - a hianyzo eszkozok mostantol maguktol letoltodnek/telepulnek."
                : "[Eszkozok] Automatikus telepites KIKAPCSOLVA - a hianyzo eszkozoket kezzel, a \"Letoltes\"/\"Telepites\" gombokkal kell megszerezni.");
            if (enabled) _ = AutoAcquireIfEnabledAsync();
        }

        private void Log(string message)
        {
            // Ha a fo ablak elerheto, oda is beirjuk (log-panel) - kulonben
            // csendben elnyeljuk (pl. tervezoi elonezet).
            if (Application.Current?.MainWindow is MainWindow mw)
                mw.LogToConsole(message);
        }

        // ───────── Lekerdezo eszkozok + RustDesk jelenlet-allapota ─────────
        private void BuildToolStatusList()
        {
            var rows = new List<UIElement>
            {
                BuildToolRow("CPU-Z", ToolAcquisition.IsPresent(ToolId.CpuZ), "portable",
                    () => ToolAcquisition.EnsureAsync(ToolId.CpuZ, Log)),
                BuildToolRow("GPU-Z", ToolAcquisition.IsPresent(ToolId.GpuZ), "portable",
                    () => ToolAcquisition.EnsureAsync(ToolId.GpuZ, Log)),
                BuildToolRow("H.D. Sentinel (FREE)", ToolAcquisition.IsPresent(ToolId.HdSentinelFree), "portable",
                    () => ToolAcquisition.EnsureAsync(ToolId.HdSentinelFree, Log)),
                BuildToolRow("Resource Hacker (csak jelenlet-ellenorzes)", ToolAcquisition.IsPresent(ToolId.ResourceHacker), "portable",
                    () => ToolAcquisition.EnsureAsync(ToolId.ResourceHacker, Log)),
                BuildRustDeskRow()
            };
            ToolStatusList.ItemsSource = rows;
        }

        private UIElement BuildRustDeskRow()
        {
            if (RustDeskLauncher.IsInstalled())
            {
                var panel = new DockPanel { Margin = new Thickness(0, 3, 0, 3), LastChildFill = false };
                panel.Children.Add(new TextBlock
                {
                    Text = "RustDesk (tavfelugyelet) - telepitve",
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("AccentNeon")
                });
                var openBtn = new Button
                {
                    Content = "Megnyitas",
                    Style = (Style)FindResource("NeonButtonStyle"),
                    MinWidth = 90,
                    Height = 28,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                openBtn.Click += (s, e) => RustDeskLauncher.LaunchExisting(Log);
                panel.Children.Add(openBtn);
                return panel;
            }

            return BuildToolRow("RustDesk (tavfelugyelet)", false, "rendszerszintu telepites - SetUpER",
                () => RustDeskLauncherWrapped());
        }

        private async Task<object> RustDeskLauncherWrapped()
        {
            var (ok, message) = await RustDeskLauncher.EnsureInstalledAsync(Log);
            Log(message);
            return ok;
        }

        // Kozos sor-epito: barmilyen eszkoz jelenlet-allapotat es egy
        // "megszerzes" muveletet jelenit meg egysegesen (portable letoltes
        // VAGY rendszerszintu telepites - a hivo donti el, melyiket adja
        // at acquireFunc-kent).
        private UIElement BuildToolRow(string label, bool present, string kindLabel, Func<Task> acquireFunc)
        {
            var panel = new DockPanel { Margin = new Thickness(0, 3, 0, 3), LastChildFill = false };

            var text = new TextBlock
            {
                Text = $"{label} - {(present ? $"telepitve ({kindLabel})" : "hianyzik")}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = present ? (Brush)FindResource("AccentNeon") : Brushes.OrangeRed
            };
            DockPanel.SetDock(text, Dock.Left);
            panel.Children.Add(text);

            if (!present)
            {
                var btn = new Button
                {
                    Content = "Telepites",
                    Style = (Style)FindResource("NeonButtonStyle"),
                    MinWidth = 90,
                    Height = 28,
                    Margin = new Thickness(10, 0, 0, 0)
                };
                DockPanel.SetDock(btn, Dock.Left);
                btn.Click += async (s, e) =>
                {
                    btn.IsEnabled = false;
                    btn.Content = "Folyamatban...";
                    await acquireFunc();
                    BuildToolStatusList();
                };
                panel.Children.Add(btn);
            }

            return panel;
        }

        // Ha az "automatikus telepites" be van kapcsolva (ToolsSettingsService),
        // a nezet MEGNYITASAKOR magatol nekilat a hianyzo eszkozok
        // megszerzesenek - a felhasznalonak nem kell kulon kattintania.
        // A tenyleges letoltes/telepites igy is ugyanazon a logikan
        // (ToolAcquisition / RustDeskLauncher) megy at, csak nem var
        // kattintasra.
        private async Task AutoAcquireIfEnabledAsync()
        {
            if (_autoInstallRunning) return;
            if (!ToolsSettingsService.Load().AutoInstall) return;

            _autoInstallRunning = true;
            try
            {
                if (!ToolAcquisition.IsPresent(ToolId.CpuZ)) { await ToolAcquisition.EnsureAsync(ToolId.CpuZ, Log); }
                if (!ToolAcquisition.IsPresent(ToolId.GpuZ)) { await ToolAcquisition.EnsureAsync(ToolId.GpuZ, Log); }
                if (!ToolAcquisition.IsPresent(ToolId.HdSentinelFree)) { await ToolAcquisition.EnsureAsync(ToolId.HdSentinelFree, Log); }
                if (!ToolAcquisition.IsPresent(ToolId.ResourceHacker)) { await ToolAcquisition.EnsureAsync(ToolId.ResourceHacker, Log); }
                if (!RustDeskLauncher.IsInstalled() && RustDeskLauncher.IsSetUpErReady())
                {
                    var (ok, message) = await RustDeskLauncher.EnsureInstalledAsync(Log);
                    Log(message);
                }
                BuildToolStatusList();
            }
            finally
            {
                _autoInstallRunning = false;
            }
        }
    }
}
