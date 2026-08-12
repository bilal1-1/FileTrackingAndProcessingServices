using FileTrackingAndProcessingServices.Models;
using Microsoft.Extensions.Options;

namespace FileTrackingAndProcessingServices.Services
{
    public class FileScanBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FolderWatchSettings _settings;
        private readonly ILogger<FileScanBackgroundService> _logger;

        public FileScanBackgroundService(
            IServiceProvider serviceProvider,
            IOptions<FolderWatchSettings> options,
            ILogger<FileScanBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Otomatik tarama servisi başlatıldı. Aralık: {Interval} saniye.",
                _settings.ScanIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var scanner = scope.ServiceProvider.GetRequiredService<IFolderScannerService>();
                        var newFileCount = await scanner.ScanFolderAsync();
                        _logger.LogInformation("Otomatik tarama tamamlandı. {Count} yeni dosya işlendi.", newFileCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Otomatik tarama sırasında hata oluştu.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_settings.ScanIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break; // uygulama kapanıyor, döngüden temiz çık
                }
            }
        }
    }
}