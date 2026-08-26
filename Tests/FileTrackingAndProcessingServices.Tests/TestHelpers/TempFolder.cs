namespace FileTrackingAndProcessingServices.Tests.TestHelpers
{
    /// <summary>
    /// Test süresince yaşayan, sonunda tamamen silinen geçici bir klasör.
    /// FolderScannerService gerçek dosya sistemiyle çalıştığı için sahte bir
    /// dosya sistemi yerine gerçek ama izole bir klasör kullanılıyor: her test
    /// kendi klasörünü alır, testler birbirinin dosyalarını görmez.
    /// </summary>
    public sealed class TempFolder : IDisposable
    {
        // Property adı bilinçli olarak "Path" DEĞİL: bu sınıfın içinde
        // System.IO.Path kullanılıyor (Path.Combine, Path.GetTempPath) ve
        // "Path" adlı bir üye tip adını gölgeleyip derlemeyi kırardı.
        public string FullPath { get; }

        public TempFolder()
        {
            FullPath = Path.Combine(Path.GetTempPath(), "dosyatakip-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FullPath);
        }

        /// <summary>
        /// Klasöre bir dosya yazar ve tam yolunu döner.
        /// İçerik UTF-8 olarak, satır sonu eklenmeden yazılır — hash'in
        /// beklenen değerle karşılaştırılabilmesi için baytların tam olarak
        /// bilinmesi gerekiyor.
        /// </summary>
        public string WriteFile(string fileName, string content)
        {
            var fullPath = Path.Combine(FullPath, fileName);
            File.WriteAllText(fullPath, content, new System.Text.UTF8Encoding(false));
            return fullPath;
        }

        public string CreateSubFolder(string name)
        {
            var path = Path.Combine(FullPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(FullPath))
                {
                    Directory.Delete(FullPath, recursive: true);
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
