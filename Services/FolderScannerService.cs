using System.Security.Cryptography;
using FileTrackingAndProcessingServices.Data;
using FileTrackingAndProcessingServices.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FileTrackingAndProcessingServices.Services
{
    public class FolderScannerService : IFolderScannerService
    {
        private readonly AppDbContext _context;
        private readonly FolderWatchSettings _settings;
        private readonly ILogger<FolderScannerService> _logger;

        public FolderScannerService(
            AppDbContext context,
            IOptions<FolderWatchSettings> options,
            ILogger<FolderScannerService> logger)
        {
            _context = context;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<int> ScanFolderAsync()
        {
            // 1. Klasör var mı kontrol et
            if (!Directory.Exists(_settings.FolderPath))
            {
                _logger.LogWarning("Taranacak klasör bulunamadı: {FolderPath}", _settings.FolderPath);
                return 0;
            }

            // 2. Klasördeki dosyaları al
            var directoryInfo = new DirectoryInfo(_settings.FolderPath);
            var files = directoryInfo.GetFiles();

            // 3. Kayıtlı dosyaların tamamını tek sorguda çek.
            // Böylece döngü içinde her dosya için ayrı sorgu atılmaz (N+1 önlenir).
            var existingFiles = await _context.TrackedFiles
                .ToDictionaryAsync(f => f.FilePath);

            int newFileCount = 0;

            // 4. Her dosyayı tek tek işle
            foreach (var file in files)
            {
                try
                {
                    // 5. Bu dosya daha önce kaydedilmiş mi? (tam yola göre tekrar kontrolü)
                    if (existingFiles.TryGetValue(file.FullName, out var existing))
                    {
                        // Boyut ya da değiştirilme tarihi farklıysa içerik değişmiş
                        // OLABİLİR — kesin bilmenin tek yolu hash'i yeniden hesaplayıp
                        // eskisiyle karşılaştırmak. Hash'i boş olan kayıtlar (Hash alanı
                        // eklenmeden önce oluşmuş olanlar) da burada doldurulur.
                        // Bu kontrol, alanlar tazelenmeden ÖNCE yapılmalı.
                        bool hashGerekli = string.IsNullOrEmpty(existing.Hash)
                            || existing.SizeBytes != file.Length
                            || existing.ModifiedAt != file.LastWriteTime;

                        if (hashGerekli)
                        {
                            var yeniHash = await ComputeHashAsync(file);

                            if (string.IsNullOrEmpty(existing.Hash))
                            {
                                _logger.LogDebug("Hash'i olmayan kayıt dolduruldu: {FileName}", file.Name);
                            }
                            else if (existing.Hash != yeniHash)
                            {
                                _logger.LogInformation("Dosya içeriği değişti: {FileName}", file.Name);
                            }
                            else
                            {
                                // Hash aynıysa içerik de aynıdır; boyut değişseydi hash de
                                // değişirdi. Demek ki hashGerekli'yi tetikleyen tek şey
                                // tarihmiş — ör. yedekten geri yükleme, dosyanın açılıp
                                // değiştirilmeden kaydedilmesi.
                                _logger.LogDebug(
                                    "Değiştirilme tarihi değişti ama içerik aynı: {FileName}", file.Name);
                            }

                            existing.Hash = yeniHash;
                        }

                        // Zaten işlenmiş — tekrar İŞLENMEZ, yeni satır açılmaz.
                        // Sadece diskteki güncel bilgisi tazelenir.
                        existing.ModifiedAt = file.LastWriteTime;
                        existing.SizeBytes = file.Length;

                        _logger.LogDebug("Dosya zaten kayıtlı, bilgisi güncellendi: {FileName}", file.Name);
                        continue;
                    }

                    // 6. Yeni dosya — bilgilerini çıkar ve kaydet
                    var trackedFile = new TrackedFile
                    {
                        FileName = file.Name,
                        FilePath = file.FullName,
                        Extension = file.Extension,
                        SizeBytes = file.Length,
                        Hash = await ComputeHashAsync(file),
                        CreatedAt = file.CreationTime,
                        ModifiedAt = file.LastWriteTime
                    };

                    _context.TrackedFiles.Add(trackedFile);
                    newFileCount++;

                    _logger.LogInformation("Yeni dosya işlendi: {FileName}", file.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dosya işlenirken hata oluştu: {FileName}", file.Name);
                }
            }

            // 7. Tüm yeni kayıtları ve güncellemeleri tek seferde veritabanına yaz
            await _context.SaveChangesAsync();

            return newFileCount;
        }

        /// <summary>
        /// Dosya içeriğinin SHA-256 özetini hesaplar.
        /// </summary>
        private static async Task<string> ComputeHashAsync(FileInfo file)
        {
            // FileShare.ReadWrite: dosya başka bir süreç tarafından yazılmak üzere
            // açık olsa bile okuyabilelim (örn. SQLite'ın açık tuttuğu .db dosyası).
            await using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            // Dosyanın tamamını belleğe almadan, akış halinde özetler.
            // Böylece 2 GB'lık bir dosya da 2 GB RAM tüketmez.
            byte[] hashBytes = await SHA256.HashDataAsync(stream);

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
