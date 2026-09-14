// Verzio: v0.5.4 - 2026-09-14
// UJ: kripto-tarcak QR-kod general (lasd Models/RtsInfo.cs WalletEntry).
// A QRCoder csomagot hasznaljuk (tiszta C#, nincs kulso fuggosege, MIT
// licenc) - a NuGet-restore ezt a GitHub Actions windows-latest runneren
// vegzi buildkor, NEM a fejlesztoi sandboxban, tehat ez nem igenyel
// semmilyen helyi telepitest a felhasznalo gepén sem (mar be van epitve
// a lefordult RTS.exe-be).
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace RTS.Services
{
    public static class QrCodeHelper
    {
        // data: a QR-ba kodolando nyers szoveg (pl. cim, vagy "bitcoin:cim").
        // pixelsPerModule: mekkora legyen egy QR-"pixel" (nagyobb = elesebb,
        // de nagyobb kep is) - 5-6 korul mar jol olvashato kis meretben is.
        public static ImageSource? GenerateQrImage(string data, int pixelsPerModule = 5)
        {
            if (string.IsNullOrWhiteSpace(data)) return null;

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                byte[] pngBytes = qrCode.GetGraphic(pixelsPerModule);

                using var ms = new MemoryStream(pngBytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                // Biztonsagi halo - ha a QR-generalas barmiert hibazik, ne
                // dontse el az egesz nezet betolteset, csak ne legyen kep.
                return null;
            }
        }
    }
}
