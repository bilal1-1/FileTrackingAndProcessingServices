using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FileTrackingAndProcessingServices.Tests.TestHelpers
{
    /// <summary>
    /// Uygulamayı bellek içinde, GERÇEK boru hattıyla ayağa kaldırır: aynı
    /// routing, aynı middleware, aynı DI kayıtları, aynı controller'lar.
    ///
    /// Neden controller'ı elle "new"lemek yerine bu: sınanmak istenen
    /// davranışların çoğu sınıfın içinde değil, boru hattında yaşıyor —
    /// bulunamayan kaydın 404'e çevrilmesi, eksik parametrenin 400 dönmesi,
    /// "duplicates" rotasının "{id}" kalıbından önce eşleşmesi, hata
    /// middleware'inin 500 üretmesi. Elle oluşturulan bir controller bunların
    /// hiçbirini kanıtlamaz.
    ///
    /// Üretim yapılandırmasından yalnızca iki şey değiştiriliyor; gerisi olduğu
    /// gibi kalıyor ki test edilen şey çalıştırılan şey olsun.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly string _watchFolderPath;

        public ApiFactory(string connectionString, string watchFolderPath)
        {
            _connectionString = connectionString;
            _watchFolderPath = watchFolderPath;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Development: Swagger kayıtları da bu ortamda ekleniyor, uygulamanın
            // normalde koştuğu ortamla aynı olsun.
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                // AddInMemoryCollection en son eklendiği için diğer kaynakları
                // (appsettings.json, appsettings.Development.json) ezer.
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Testcontainers'ın açtığı gerçek PostgreSQL. Uygulama
                    // açılışta migration'ları buraya uygular.
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,

                    // Taranacak klasör teste ait geçici klasör olmalı; aksi halde
                    // uygulama depo kökündeki watched/ klasörünü tarar ve testler
                    // kendi kurmadıkları kayıtları görürdü.
                    ["WatchSettings:FolderPath"] = _watchFolderPath,
                    ["WatchSettings:ScanIntervalSeconds"] = "3600"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Arka plan tarama servisi kaldırılıyor.
                //
                // Kalsaydı uygulama ayağa kalkar kalkmaz kendiliğinden tarama
                // yapar ve testin kurduğu veriye kayıt eklerdi; üstelik bu her
                // koşuda aynı anda olmayacağı için testler ara ara ve
                // açıklanamaz şekilde düşerdi. Tarama davranışı zaten
                // FolderScannerServiceTests içinde ayrıca sınanıyor.
                services.RemoveAll<IHostedService>();
            });
        }
    }
}
