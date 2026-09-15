// Verzio: v1.0.0 - 2026-09-15
// UJ SZOLGALTATAS (round16 javitas, LordAthis 2026-09-15-i pontositasa
// alapjan): a passziv, csak-olvaso hardver-lekerdezo segedeszkozok
// (CPU-Z, GPU-Z, H.D. Sentinel FREE, Resource Hacker) mostantol FELTETEL
// NELKUL, nema modon, MAR AZ RTS INDULASAKOR a hatterben beszerzodnek -
// nincs hozzajuk sem felugro ablak, sem jelolonegyzet, mert ezek sajat
// celjuk szerint artalmatlan, portable, csak-olvaso segedprogramok
// ("a tobbit automatikusan lehet telepiteni").
//
// A RustDesk KULONBOZIK: tavoli hozzaferest biztosito, rendszerszintu
// telepitest igenylo eszkoz - ezert MARADT az elso-inditasi Igen/Nem
// kerdes + a csavarkulcs-panelen levo, KIZAROLAG RustDeskre vonatkozo
// jelolonegyzet+Alkalmaz (lasd ToolsSettingsService.RustDeskAutoInstall
// es MainWindow.xaml.cs). Ezt a hivo (RustDesk eseten) csak akkor
// telepiti/allitja be automatikusan, ha ez a beallitas be van kapcsolva.
//
// Ezt a metodust MainWindow induláskor hivja (fire-and-forget), ES a
// ToolsView is meghivja panel-megnyitaskor (idempotens - ha mar minden
// megvan, vagy mar fut egy peldany, azonnal visszater, nem inditja ujra).
using System;
using System.Threading.Tasks;

namespace RTS.Services
{
    public static class ToolsBootstrap
    {
        private static bool _running;

        public static async Task RunSilentlyAsync(Action<string> log)
        {
            if (_running) return;
            _running = true;
            try
            {
                if (!ToolAcquisition.IsPresent(ToolId.CpuZ))
                    await ToolAcquisition.EnsureAsync(ToolId.CpuZ, log);
                if (!ToolAcquisition.IsPresent(ToolId.GpuZ))
                    await ToolAcquisition.EnsureAsync(ToolId.GpuZ, log);
                if (!ToolAcquisition.IsPresent(ToolId.HdSentinelFree))
                    await ToolAcquisition.EnsureAsync(ToolId.HdSentinelFree, log);
                if (!ToolAcquisition.IsPresent(ToolId.ResourceHacker))
                    await ToolAcquisition.EnsureAsync(ToolId.ResourceHacker, log);

                if (ToolsSettingsService.Load().RustDeskAutoInstall
                    && !RustDeskLauncher.IsInstalled()
                    && RustDeskLauncher.IsSetUpErReady())
                {
                    var (ok, message) = await RustDeskLauncher.EnsureInstalledAsync(log);
                    log(message);
                }
            }
            finally
            {
                _running = false;
            }
        }
    }
}
