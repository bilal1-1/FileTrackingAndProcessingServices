using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace FileTrackingAndProcessingServices.Infrastructure.FileSystem
{
    public class FolderScannerService : IFolderScannerService
    {
        // ADIM 1: "Bu sınıf şunlara ihtiyaç duyacak" diye ilan ediyoruz
        private readonly IFileRepository _repository; // db erişimini burası sağlıyor.
        private readonly IUnitOfWork _unitOfWork; // biriken değişiklikleri burası yazıyor.
        private readonly FolderWatchSettings _settings; // klasör takip ayarlarını burası sağlıyor.
        private readonly ILogger<FolderScannerService> _logger; // log tutulmasını burası sağlıyor.

        // ADIM 2: .NET bu sınıfı oluştururken buraya geliyor
        // ve gerekli nesneleri dışarıdan teslim ediyor
        public FolderScannerService(
            IFileRepository repository,
            IUnitOfWork unitOfWork,
            IOptions<FolderWatchSettings> options,
            ILogger<FolderScannerService> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
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

            // NOT — tarihlerde neden hep ...Utc uçları kullanılıyor:
            // CreationTime/LastWriteTime yerel saatli (Kind=Local) DateTime döndürür.
            // Npgsql, DateTime'ı PostgreSQL'in "timestamp with time zone" tipine
            // eşler ve bu tipe yerel saatli bir değer yazılmasına İZİN VERMEZ,
            // doğrudan hata fırlatır. SQLite tarihi metin olarak sakladığı için
            // hiç şikayet etmiyordu; sorun geçişle birlikte ortaya çıktı.
            // Ayrıca UTC saklamak doğru pratik: sunucunun saat dilimi değişse ya da
            // uygulama başka bir bölgede çalışsa bile kayıtlı an aynı kalır.

            // 3. Kayıtlı dosyaların tamamını tek sorguda çek.
            // Böylece döngü içinde her dosya için ayrı sorgu atılmaz (N+1 önlenir).
            var existingFiles = await _repository.GetAllByPathAsync();

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
                        bool shouldRecomputeHash = string.IsNullOrEmpty(existing.Hash)
                            || existing.SizeBytes != file.Length
                            || existing.ModifiedAt != file.LastWriteTimeUtc;

                        if (shouldRecomputeHash)
                        {
                            // "computed" adı bilinçli: bu değer henüz "yeni" değil,
                            // sadece taze hesaplanmış olan. Aşağıdaki karşılaştırma
                            // eskisiyle aynı çıkarsa içerik hiç değişmemiş demektir.
                            var computedHash = await ComputeHashAsync(file);

                            if (string.IsNullOrEmpty(existing.Hash))
                            {
                                _logger.LogDebug("Hash'i olmayan kayıt dolduruldu: {FileName}", file.Name);
                            }
                            else if (existing.Hash != computedHash)
                            {
                                _logger.LogInformation("Dosya içeriği değişti: {FileName}", file.Name);
                            }
                            else
                            {
                                // Hash aynıysa içerik de aynıdır; boyut değişseydi hash de
                                // değişirdi. Demek ki shouldRecomputeHash'i tetikleyen tek
                                // şey tarihmiş — ör. yedekten geri yükleme, dosyanın açılıp
                                // değiştirilmeden kaydedilmesi.
                                _logger.LogDebug(
                                    "Değiştirilme tarihi değişti ama içerik aynı: {FileName}", file.Name);
                            }

                            existing.Hash = computedHash;

                            // Zaten işlenmiş — tekrar İŞLENMEZ, yeni satır açılmaz.
                            // Sadece diskteki güncel bilgisi tazelenir.
                            existing.ModifiedAt = file.LastWriteTimeUtc;
                            existing.SizeBytes = file.Length;

                            // Güncelleme niyeti burada açıkça bildiriliyor.
                            // Bu satır olmadan da çalışırdı, çünkü EF Core
                            // repository'den dönen kaydı izliyor ve değişikliği
                            // kendisi fark ediyor. Ama o zaman kodda güncellemenin
                            // yapıldığını söyleyen hiçbir ifade olmaz ve davranış,
                            // sorgunun takip açık olmasına sessizce bağlı kalırdı.
                            _repository.Update(existing);

                            _logger.LogDebug("Dosya zaten kayıtlı, bilgisi güncellendi: {FileName}", file.Name);
                        }
                        else
                        {
                            // Boyut, tarih ve hash aynı: yazılacak bir şey yok,
                            // veritabanına hiç dokunulmuyor. Tarama sık koştuğu
                            // için değişmemiş dosyalara UPDATE atmamak önemli.
                            _logger.LogDebug("Dosya zaten kayıtlı, değişiklik yok: {FileName}", file.Name);
                        }

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
                        CreatedAt = file.CreationTimeUtc,
                        ModifiedAt = file.LastWriteTimeUtc
                    };

                    await _repository.AddAsync(trackedFile);
                    newFileCount++;

                    _logger.LogInformation("Yeni dosya işlendi: {FileName}", file.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dosya işlenirken hata oluştu: {FileName}", file.Name);
                }
            }

            // 7. Tüm yeni kayıtları ve güncellemeleri tek seferde veritabanına yaz.
            // Kaydetme anını repository değil bu servis belirliyor: döngüde biriken
            // ekleme ve güncellemelerin hepsi tek turda yazılıyor.
            await _unitOfWork.SaveChangesAsync();

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
