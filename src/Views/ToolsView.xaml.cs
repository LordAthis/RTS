// Verzio: v2.1.0 - 2026-09-16
// ROUND17 ATALAKITAS - az Eszkozok (csavarkulcs) panel logikaja.
//
// LordAthis 2026-09-16-i kerese, szo szerint teljesitve:
//
//  a) "Amennyiben a csavarkulcs ikonra nyomva a megjeleno panelen az
//      'Informaciok frissitese' gombra nyomunk, akkor a jelentest generalo
//      .ps1 fut le csendesen, ami frissiti az elmentett adatokat, es kiirja
//      a megfelelo helyre/helyekre!"
//     -> BtnRefresh_Click MOSTANTOL KIZAROLAG a lekerdezest inditja
//        (HardwareQueryService.RefreshAsync). Beszerzes/telepites INNEN
//        SOHA nem indul.
//
//  b) "ami nincsen telepitve, amelle oda lehet tenni a telepites gombot, ha
//      telepitve van, akkor a megnyitas gombot... (Ezt csak 1-1 esetben
//      tetted melle, a felenel hianyzik!)"
//     -> BuildToolRow MINDEN sorhoz allapotfuggo gombot ad: hianyzik ->
//        "Telepites", telepitve -> "Megnyitas".
//
//  c) "ha telepitve van, akkor ugyan tovabb lep, de egy masik ellenorzo
//      rutint is indit, hogy van-e frissebb verzio! Ebben az esetben a
//      megnyitas mellett a frissites jelenjen meg pluszban!"
//     -> A lista felepitese utan HATTERBEN elindul a verzio-ellenorzes
//        (ToolAcquisition.CheckUpdateAsync), es ha van ujabb verzio, a sor
//        kiegeszul egy "Frissites" gombbal. A panel ettol NEM akad meg.
//
//  d) "Ebben az esetben azt is melle tehetnenk, hogy uj gyorsjelentes.
//      (Ez csak ezt a komponenst kerdezne le ujra!)"
//     -> Minden lekerdezesben reszt vevo eszkoz sora kap egy "Uj
//        gyorsjelentes" gombot, ami KIZAROLAG az adott szekciot frissiti
//        (HwRefreshScope.Cpu / Gpu / Disk).
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
        public ToolsView()
        {
            InitializeComponent();
            LoadFromCache();
            ChkAutoInstall.IsChecked = ToolsSettingsService.Load().RustDeskAutoInstall;
            BuildToolStatusList();

            // A panel megnyitasa NEM indit beszerzest es NEM indit
            // lekerdezest - csak az allapotot frissiti a hatterben, es
            // megnezi, van-e ujabb verzio a telepitett eszkozokhoz.
            _ = RefreshStatusesAsync();
        }

        // ─────────────────────────── Megjelenites ───────────────────────────
        private void LoadFromCache()
        {
            Render(HardwareQueryService.LoadCached());
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

            var lines = new List<string>();
            if (info.System != null)
            {
                if (!string.IsNullOrWhiteSpace(info.System.MachineName)) lines.Add($"Gep: {info.System.MachineName}");
                if (!string.IsNullOrWhiteSpace(info.System.OsCaption))
                    lines.Add($"OS: {info.System.OsCaption} ({info.System.OsVersion})");
                if (info.System.RamTotalGb > 0) lines.Add($"Memoria: {info.System.RamTotalGb} GB");
            }
            if (info.Cpu != null && !string.IsNullOrWhiteSpace(info.Cpu.Summary)) lines.Add($"CPU: {info.Cpu.Summary}");
            if (info.Gpu != null && !string.IsNullOrWhiteSpace(info.Gpu.Summary)) lines.Add($"GPU: {info.Gpu.Summary}");
            if (info.Disk != null)
            {
                string disk = info.Disk.HealthPercent.HasValue ? $"{info.Disk.HealthPercent}%" : "(ismeretlen)";
                lines.Add($"Lemez-egeszseg: {disk}");
            }
            if (info.Sensors != null && !string.IsNullOrWhiteSpace(info.Sensors.Summary))
                lines.Add(info.Sensors.Summary);

            TxtSummary.Text = lines.Count > 0 ? string.Join("\n", lines) : "Nincs adat.";

            TxtCpuRaw.Text    = SectionText(info.Cpu);
            TxtGpuRaw.Text    = SectionText(info.Gpu);
            TxtDiskRaw.Text   = SectionText(info.Disk);
            TxtSystemRaw.Text = SectionText(info.System);
            TxtSensorsRaw.Text = SectionText(info.Sensors);
            TxtDxDiagRaw.Text = info.DxDiag == null
                ? "Meg nem tortent DXDIAG lekerdezes - hasznald a fenti \"DXDIAG lekerdezese\" gombot."
                : SectionText(info.DxDiag);
        }

        // Egy szekcio nyers szovege + a forras megjelolese (hogy lathato
        // legyen, kell-e egyaltalan a kulso eszkoz a pontosabb adathoz).
        private static string SectionText(HwSectionBase? section)
        {
            if (section == null) return "Nincs adat.";
            string text = !string.IsNullOrWhiteSpace(section.RawText) ? section.RawText : "Nincs adat.";
            if (!string.IsNullOrWhiteSpace(section.RawSource))
            {
                text = $"[Adatforras: {section.RawSource}]\r\n\r\n" + text;
            }
            if (section.Errors is { Count: > 0 })
            {
                text += "\r\n\r\n[Figyelmeztetesek]\r\n" + string.Join("\r\n", section.Errors);
            }
            return text;
        }

        // ─────────────────────────── Lekerdezes ───────────────────────────
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RunQueryAsync(HwRefreshScope.All, BtnRefresh, "Informaciok frissitese");
        }

        private async void BtnDxDiag_Click(object sender, RoutedEventArgs e)
        {
            await RunQueryAsync(HwRefreshScope.DxDiag, BtnDxDiag, "DXDIAG lekerdezese");
        }

        // Kozos lekerdezes-futtato. CSAK lekerdez - SEMMIT nem telepit.
        private async Task RunQueryAsync(HwRefreshScope scope, Button? button, string originalContent)
        {
            if (button != null)
            {
                button.IsEnabled = false;
                button.Content = "Folyamatban...";
            }

            TxtLastQueried.Text = scope == HwRefreshScope.DxDiag
                ? "DXDIAG lekerdezes folyamatban - ez 15-40 masodpercig is eltarthat..."
                : "Lekerdezes folyamatban (csendben, a hatterben)...";

            try
            {
                var info = await HardwareQueryService.RefreshAsync(scope, Log);
                Render(info);
                RefreshB2Summary();
            }
            catch (Exception ex)
            {
                Log("[Eszkozok] Hiba a lekerdezes soran: " + ex.Message);
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = originalContent;
                }
            }
        }

        // A B2 doboz (fo ablak jobb oldalan) osszefoglalojanak frissitese,
        // hogy a friss adat ott is azonnal latszodjon.
        private void RefreshB2Summary()
        {
            try
            {
                if (Application.Current?.MainWindow is MainWindow mw
                    && mw.FindName("B2Content") is ContentControl cc
                    && cc.Content is ToolsSummaryView summary)
                {
                    summary.Load();
                }
            }
            catch { /* nem kritikus */ }
        }

        // ─────────────────────────── RustDesk kapcsolo ───────────────────────────
        private void BtnApplyAutoInstall_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = ChkAutoInstall.IsChecked == true;
            ToolsSettingsService.Save(enabled);
            Log(enabled
                ? "[RustDesk] Automatikus telepites es beallitas BEKAPCSOLVA."
                : "[RustDesk] Automatikus telepites es beallitas KIKAPCSOLVA - a RustDesk mostantol csak a sajat sora melletti \"Telepites\" gombbal telepszik.");
        }

        // ROUND18 - LordAthis 2026-09-16-i kerese: "Plusz gomb (...), az
        // osszegyujtott gepadatok elkuldese... (DiagMailer-el mehet ez is a
        // beallitott cimre!)". A tenyleges osszeallitast es kuldest a
        // DiagMailerLauncher.SendHardwareReport vegzi.
        private void BtnSendReport_Click(object sender, RoutedEventArgs e)
        {
            BtnSendReport.IsEnabled = false;
            try
            {
                var (ok, message) = DiagMailerLauncher.SendHardwareReport(Log);
                Log(message);
            }
            catch (Exception ex)
            {
                Log("[DiagMailer] Hiba a hardver-riport kuldesekor: " + ex.Message);
            }
            finally
            {
                BtnSendReport.IsEnabled = true;
            }
        }

        private void BtnRecheckTools_Click(object sender, RoutedEventArgs e)
        {
            BuildToolStatusList();
            _ = RefreshStatusesAsync();
        }

        private void Log(string message)
        {
            if (Application.Current?.MainWindow is MainWindow mw)
                mw.LogToConsole(message);
        }

        // ───────────── Eszkozok allapota + allapotfuggo gombok ─────────────
        private readonly Dictionary<ToolId, ToolUpdateStatus> _updateStatuses = new();

        private void BuildToolStatusList()
        {
            var rows = new List<UIElement>();

            // A megjelenites sorrendje: eloszor a lekerdezesben reszt vevo
            // eszkozok, majd a tobbi. (LordAthis 2026-09-16: "Plusz ket
            // program beilleszteni a Csavarkulcs panelre" - a
            // LibreHardwareMonitor es a HWMonitor is itt jelenik meg.)
            foreach (ToolId tool in new[]
                     {
                         ToolId.LibreHardwareMonitor,
                         ToolId.CpuZ,
                         ToolId.GpuZ,
                         ToolId.HdSentinelFree,
                         ToolId.HwMonitor,
                         ToolId.ResourceHacker
                     })
            {
                rows.Add(BuildToolRow(tool));
            }
            rows.Add(BuildRustDeskRow());

            ToolStatusList.ItemsSource = rows;
        }

        // A telepitett eszkozokhoz a HATTERBEN ellenorizzuk, van-e ujabb
        // verzio - ha igen, a sor ujraepul egy "Frissites" gombbal is.
        private async Task RefreshStatusesAsync()
        {
            try
            {
                bool anyChange = false;
                foreach (ToolId tool in ToolDetection.Catalog.Keys)
                {
                    // Frissites-ellenorzes csak a winget-kezelt, TELEPITETT
                    // eszkozoknel ertelmes.
                    if (ToolDetection.Describe(tool).WingetIds.Length == 0) continue;
                    if (!ToolAcquisition.IsPresent(tool)) continue;

                    var status = await ToolAcquisition.CheckUpdateAsync(tool, null);
                    _updateStatuses[tool] = status;
                    if (status.UpdateAvailable) anyChange = true;
                }

                // Az allapotot a beallito-fajlba is rogzitjuk (a kovetkezo
                // indulas mar ebbol dolgozik, ujraellenorzes nelkul).
                ToolsBootstrap.RefreshPresenceStates();

                if (anyChange) BuildToolStatusList();
            }
            catch { /* a verzio-ellenorzes sosem allithatja meg a panelt */ }
        }

        private UIElement BuildToolRow(ToolId tool)
        {
            var desc = ToolDetection.Describe(tool);
            var presence = ToolDetection.Detect(tool);

            var outer = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };

            // ── Felso sor: nev + allapot ──
            var header = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = presence.Installed
                    ? (Brush)FindResource("AccentNeon")
                    : Brushes.OrangeRed
            };
            string versionText = string.IsNullOrWhiteSpace(presence.Version) ? "" : $" v{presence.Version}";
            header.Text = presence.Installed
                ? $"{desc.DisplayName}{versionText} - {presence.Source}"
                : $"{desc.DisplayName} - nincs telepitve";
            outer.Children.Add(header);

            if (!string.IsNullOrWhiteSpace(desc.Note))
            {
                outer.Children.Add(new TextBlock
                {
                    Text = desc.Note,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 1, 0, 0)
                });
            }

            // ── Also sor: allapotfuggo gombok ──
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            if (!presence.Installed)
            {
                // Nincs telepitve -> "Telepites"
                var btnInstall = MakeButton("Telepites", 100);
                btnInstall.Click += async (s, e) =>
                {
                    btnInstall.IsEnabled = false;
                    btnInstall.Content = "Folyamatban...";
                    var result = await ToolAcquisition.EnsureAsync(tool, Log);
                    Log("[Eszkozok] " + result.Message);

                    // Ha az automatikus telepites nem ment, NEM nyitunk meg
                    // magatol semmit - csak felajanljuk a letoltesi oldalt
                    // egy KULON gombbal (lasd lentebb).
                    BuildToolStatusList();
                };
                buttons.Children.Add(btnInstall);

                // "Letoltesi oldal" - CSAK a felhasznalo kattintasara nyilik
                // meg. (A round16-os automatikus megnyitas volt az oka, hogy
                // "allandoan megnyitja a techpower oldalat".)
                if (!string.IsNullOrWhiteSpace(ToolAcquisition.DownloadPageUrl(tool)))
                {
                    var btnPage = MakeButton("Letoltesi oldal", 130);
                    btnPage.Click += (s, e) => ToolAcquisition.OpenDownloadPage(tool, Log);
                    buttons.Children.Add(btnPage);
                }
            }
            else
            {
                // Telepitve -> "Megnyitas"
                var btnOpen = MakeButton("Megnyitas", 100);
                btnOpen.Click += (s, e) => ToolAcquisition.LaunchExisting(tool, Log);
                buttons.Children.Add(btnOpen);

                // ... es ha van ujabb verzio, PLUSZBAN "Frissites"
                if (_updateStatuses.TryGetValue(tool, out var status) && status.UpdateAvailable)
                {
                    string label = string.IsNullOrWhiteSpace(status.AvailableVersion)
                        ? "Frissites"
                        : $"Frissites -> {status.AvailableVersion}";
                    var btnUpdate = MakeButton(label, 150);
                    btnUpdate.Click += async (s, e) =>
                    {
                        btnUpdate.IsEnabled = false;
                        btnUpdate.Content = "Folyamatban...";
                        var result = await ToolAcquisition.UpdateAsync(tool, Log);
                        Log("[Eszkozok] " + result.Message);
                        _updateStatuses.Remove(tool);
                        BuildToolStatusList();
                        _ = RefreshStatusesAsync();
                    };
                    buttons.Children.Add(btnUpdate);
                }
            }

            // "Uj gyorsjelentes" - csak azoknal az eszkozoknel, amelyek
            // reszt vesznek a lekerdezesben (a Resource Hacker nem).
            HwRefreshScope? scope = ScopeForTool(tool);
            if (scope.HasValue)
            {
                var btnQuick = MakeButton("Uj gyorsjelentes", 145);
                btnQuick.Click += async (s, e) =>
                {
                    await RunQueryAsync(scope.Value, btnQuick, "Uj gyorsjelentes");
                };
                buttons.Children.Add(btnQuick);
            }

            outer.Children.Add(buttons);
            return outer;
        }

        private static HwRefreshScope? ScopeForTool(ToolId tool) => tool switch
        {
            ToolId.CpuZ           => HwRefreshScope.Cpu,
            ToolId.GpuZ           => HwRefreshScope.Gpu,
            ToolId.HdSentinelFree => HwRefreshScope.Disk,
            ToolId.LibreHardwareMonitor => HwRefreshScope.Sensors,
            _                     => null
        };

        private UIElement BuildRustDeskRow()
        {
            var outer = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            bool installed = RustDeskLauncher.IsInstalled();

            outer.Children.Add(new TextBlock
            {
                Text = installed
                    ? "RustDesk (tavfelugyelet) - telepitve"
                    : "RustDesk (tavfelugyelet) - nincs telepitve",
                TextWrapping = TextWrapping.Wrap,
                Foreground = installed ? (Brush)FindResource("AccentNeon") : Brushes.OrangeRed
            });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            if (installed)
            {
                var btnOpen = MakeButton("Megnyitas", 100);
                btnOpen.Click += (s, e) => RustDeskLauncher.LaunchExisting(Log);
                buttons.Children.Add(btnOpen);
            }
            else
            {
                var btnInstall = MakeButton("Telepites", 100);
                btnInstall.Click += async (s, e) =>
                {
                    btnInstall.IsEnabled = false;
                    btnInstall.Content = "Folyamatban...";
                    var (ok, message) = await RustDeskLauncher.EnsureInstalledAsync(Log);
                    Log(message);
                    BuildToolStatusList();
                };
                buttons.Children.Add(btnInstall);
            }

            outer.Children.Add(buttons);
            return outer;
        }

        private Button MakeButton(string content, double width)
        {
            return new Button
            {
                Content = content,
                Style = (Style)FindResource("NeonButtonStyle"),
                MinWidth = width,
                Height = 28,
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0)
            };
        }
    }
}
