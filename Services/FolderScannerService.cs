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
            // 4. Bu dosya daha önce kaydedilmiş mi? (tekrar kontrolü)
            bool alreadyExists = await _context.TrackedFiles
                .AnyAsync(f => f.FileName == file.Name && f.ModifiedAt == file.LastWriteTime);

            if (alreadyExists)
            {
                continue; // zaten var, atla
            }

            // 5. Yeni dosya — bilgilerini çıkar ve kaydet
            var trackedFile = new TrackedFile
            {
                FileName = file.Name,
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