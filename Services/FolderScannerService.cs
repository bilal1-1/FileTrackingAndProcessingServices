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

    int newFileCount = 0;

    // 3. Her dosyayı tek tek işle
    foreach (var file in files)
    {
        try
        {
            // 4. Bu dosya daha önce kaydedilmiş mi? (tam yola göre tekrar kontrolü)
            var existing = await _context.TrackedFiles
                .FirstOrDefaultAsync(f => f.FilePath == file.FullName);

            if (existing != null)
            {
                // Zaten işlenmiş — tekrar İŞLENMEZ, yeni satır açılmaz.
                // Sadece diskteki güncel bilgisi tazelenir.
                existing.ModifiedAt = file.LastWriteTime;
                existing.SizeBytes = file.Length;

                _logger.LogInformation("Dosya zaten kayıtlı, bilgisi güncellendi: {FileName}", file.Name);
                continue;
            }

            // 5. Yeni dosya — bilgilerini çıkar ve kaydet
            var trackedFile = new TrackedFile
            {
                FileName = file.Name,
                FilePath = file.FullName,
                Extension = file.Extension,
                SizeBytes = file.Length,
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

    // 6. Tüm yeni dosyaları tek seferde veritabanına yaz
    await _context.SaveChangesAsync();

    return newFileCount;
}
    }
}