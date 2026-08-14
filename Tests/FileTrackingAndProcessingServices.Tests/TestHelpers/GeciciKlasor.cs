namespace FileTrackingAndProcessingServices.Tests.TestHelpers
{
    /// <summary>
    /// Test süresince yaşayan, sonunda tamamen silinen geçici bir klasör.
    /// FolderScannerService gerçek dosya sistemiyle çalıştığı için sahte bir
    /// dosya sistemi yerine gerçek ama izole bir klasör kullanılıyor: her test
    /// kendi klasörünü alır, testler birbirinin dosyalarını görmez.
    /// </summary>
    public sealed class GeciciKlasor : IDisposable
    {
        public string Yol { get; }

        public GeciciKlasor()
        {
            Yol = Path.Combine(Path.GetTempPath(), "dosyatakip-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Yol);
        }

        /// <summary>
        /// Klasöre bir dosya yazar ve tam yolunu döner.
        /// İçerik UTF-8 olarak, satır sonu eklenmeden yazılır — hash'in
        /// beklenen değerle karşılaştırılabilmesi için baytların tam olarak
        /// bilinmesi gerekiyor.
        /// </summary>
        public string DosyaYaz(string dosyaAdi, string icerik)
        {
            var tamYol = Path.Combine(Yol, dosyaAdi);
            File.WriteAllText(tamYol, icerik, new System.Text.UTF8Encoding(false));
            return tamYol;
        }

        public string AltKlasorOlustur(string ad)
        {
            var yol = Path.Combine(Yol, ad);
            Directory.CreateDirectory(yol);
            return yol;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Yol))
                {
                    Directory.Delete(Yol, recursive: true);
                }
            }
            catch (IOException)
            {
                // Test bitiminde dosya hâlâ kilitliyse temizlik başarısız olabilir.
                // Geçici klasör olduğu için bu, testi düşürmeyi hak etmiyor.
            }
        }
    }
}
