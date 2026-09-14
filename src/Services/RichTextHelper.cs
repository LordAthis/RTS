// Verzio: v0.5.4 - 2026-09-14
// Ujrafelhasznalhato segedosztaly: egy szovegben talalhato http(s):// linkeket
// kattinthato Hyperlink-kent jeleniti meg egy TextBlock-ban (a rendszer
// alapertelmezett bongeszojeben nyilnak meg), a tobbi resz sima szoveg marad.
// Korabban ez MainWindow.xaml.cs-ben elt (csak a B2-hoz), most kulon Service,
// hogy barmelyik View (pl. HomeInfoView, kesobb mas nezetek is) hasznalhassa.
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RTS.Services
{
    public static class RichTextHelper
    {
        private static readonly Regex UrlPattern = new Regex(@"https?://[^\s]+", RegexOptions.Compiled);

        // onLinkError: opcionalis callback, ha a link megnyitasa hibazik
        // (pl. mainWin.LogToConsole) - ha nincs megadva, csendben elnyeli.
        public static TextBlock BuildTextBlockWithLinks(string text, Brush foreground, double fontSize, Action<string>? onLinkError = null)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontSize = fontSize,
                Foreground = foreground
            };

            int lastIndex = 0;
            foreach (Match m in UrlPattern.Matches(text))
            {
                if (m.Index > lastIndex)
                    tb.Inlines.Add(new Run(text.Substring(lastIndex, m.Index - lastIndex)));

                // Vegen levo irasjelek (pont, vesszo, zarojel) NEM resze a
                // linknek - sok mondat URL-lel vegzodik, azt nem akarjuk
                // belevonni a kattinthato reszbe.
                string rawUrl = m.Value;
                string trimmed = rawUrl.TrimEnd('.', ',', ')', ';', '!', '?');
                string trailing = rawUrl.Substring(trimmed.Length);

                if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
                {
                    var link = new Hyperlink(new Run(trimmed))
                    {
                        NavigateUri = uri,
                        Foreground = (Brush)Application.Current.Resources["AccentNeon"]
                    };
                    link.RequestNavigate += (s, e) =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = e.Uri.AbsoluteUri,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            onLinkError?.Invoke("Hiba a link megnyitasakor: " + ex.Message);
                        }
                    };
                    tb.Inlines.Add(link);
                    if (!string.IsNullOrEmpty(trailing))
                        tb.Inlines.Add(new Run(trailing));
                }
                else
                {
                    // Ervenytelen URI (nem varhato, de biztonsagi halo) - sima
                    // szovegkent jelenik meg, nem all elo kattinthato hibas link.
                    tb.Inlines.Add(new Run(rawUrl));
                }

                lastIndex = m.Index + rawUrl.Length;
            }
            if (lastIndex < text.Length)
                tb.Inlines.Add(new Run(text.Substring(lastIndex)));

            return tb;
        }
    }
}
