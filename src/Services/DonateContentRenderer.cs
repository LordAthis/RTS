// Verzio: v0.5.5 - 2026-09-14
// Megosztott renderelo logika a donate.json tartalomhoz - ezt hasznalja
// mind a HomeInfoView (ahol az RTS-info.json ALA, kulon szekciokent
// jelenik meg, elvalaszto vonallal), mind az uj DonateView (ahol EZ az
// egyetlen, teljes ertekű tartalom, nagyobb cimmel, elvalaszto vonal
// nelkul) - igy nem kell ketszer megirni ugyanazt a megjelenitesi logikat.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RTS.Models;

namespace RTS.Services
{
    public static class DonateContentRenderer
    {
        // isSubSection = true: a HomeInfoView-ban, az RTS-info.json alatt
        //   jelenik meg -> elvalaszto vonal felette, kisebb cim-betumeret.
        // isSubSection = false: a DonateView-ban, ONALLO tartalomkent
        //   jelenik meg -> nincs elvalaszto vonal, nagyobb cim-betumeret
        //   (ugyanaz a stilus, mint a HomeInfoView fo cimenel).
        public static void Render(Panel target, DonateInfo info, bool isSubSection, Action<string>? onError = null)
        {
            bool hasContent = !string.IsNullOrWhiteSpace(info.Title) || info.Lines.Count > 0 || info.Wallets.Count > 0;
            if (!hasContent) return;

            if (isSubSection)
            {
                target.Children.Add(new Border
                {
                    BorderBrush = (Brush)Application.Current.Resources["PanelBorderBrush"],
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin = new Thickness(0, 10, 0, 16)
                });
            }

            if (!string.IsNullOrWhiteSpace(info.Title))
            {
                target.Children.Add(new TextBlock
                {
                    Text = info.Title,
                    Foreground = (Brush)Application.Current.Resources[isSubSection ? "AccentNeon" : "SecondaryNeon"],
                    FontWeight = FontWeights.Bold,
                    FontSize = isSubSection ? 16 : 22,
                    Margin = new Thickness(0, 0, 0, isSubSection ? 10 : 16),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
            }

            foreach (var line in info.Lines)
            {
                var tb = RichTextHelper.BuildTextBlockWithLinks(line, (Brush)Application.Current.Resources["TextBrush"], isSubSection ? 13 : 14, onError);
                tb.Margin = new Thickness(0, 0, 0, isSubSection ? 8 : 10);
                target.Children.Add(tb);
            }

            foreach (var wallet in info.Wallets)
            {
                if (string.IsNullOrWhiteSpace(wallet.Address)) continue;
                target.Children.Add(BuildWalletBlock(wallet));
            }
        }

        private static UIElement BuildWalletBlock(WalletEntry wallet)
        {
            string qrData = string.IsNullOrWhiteSpace(wallet.UriScheme)
                ? wallet.Address
                : $"{wallet.UriScheme}:{wallet.Address}";

            var container = new Border
            {
                Background = (Brush)Application.Current.Resources["PanelBgBrush"],
                BorderBrush = (Brush)Application.Current.Resources["PanelBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            if (!string.IsNullOrWhiteSpace(wallet.Label))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = wallet.Label,
                    Foreground = (Brush)Application.Current.Resources["AccentNeon"],
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            // Verzio v0.5.7 - 2026-09-14: ha a "cim" mezo valojaban egy
            // http(s) URL (pl. Revolut/PayPal/Patreon/Skool linkek, nem
            // kripto-tarca), akkor KATTINTHATO linkkent jelenitjuk meg -
            // egy kripto-cimet viszont soha nem "kattintjuk meg", azt
            // masoljak/scannelik, ezert marad sima monospace szoveg.
            bool isLink = wallet.Address.StartsWith("http://") || wallet.Address.StartsWith("https://");
            if (isLink)
            {
                var linkTb = RichTextHelper.BuildTextBlockWithLinks(wallet.Address, (Brush)Application.Current.Resources["TextBrush"], 12);
                linkTb.TextAlignment = TextAlignment.Center;
                linkTb.HorizontalAlignment = HorizontalAlignment.Center;
                linkTb.Margin = new Thickness(0, 0, 0, 8);
                stack.Children.Add(linkTb);
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text = wallet.Address,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            var qrImage = QrCodeHelper.GenerateQrImage(qrData);
            if (qrImage != null)
            {
                var img = new Image
                {
                    Source = qrImage,
                    Width = 140,
                    Height = 140,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                stack.Children.Add(img);
            }
            else
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "(QR-kod generalasa nem sikerult)",
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            container.Child = stack;
            return container;
        }
    }
}
