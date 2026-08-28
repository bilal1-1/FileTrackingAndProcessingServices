using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using Microsoft.Extensions.Options;

namespace FileTrackingAndProcessingServices.WebApi.BackgroundServices
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
                        // stoppingToken aşağı geçiriliyor: uygulama kapanırken
                        // (docker compose down) süren tarama yarıda bırakılabilsin.
                        var newFileCount = await scanner.ScanFolderAsync(stoppingToken);
                        _logger.LogInformation("Otomatik tarama tamamlandı. {Count} yeni dosya işlendi.", newFileCount);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Uygulama kapanırken tarama yarıda kesildi. Bu bir hata
                    // değil; aşağıdaki genel catch yakalasaydı her kapanışta
                    // loglara hata satırı düşerdi.
                    _logger.LogInformation("Otomatik tarama, uygulama kapandığı için yarıda bırakıldı.");
                    break;
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
