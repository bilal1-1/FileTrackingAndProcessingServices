using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Infrastructure.FileSystem;
using FileTrackingAndProcessingServices.Infrastructure.Persistence.Repositories;
using FileTrackingAndProcessingServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileTrackingAndProcessingServices.Infrastructure
{
    /// <summary>
    /// Infrastructure'daki somut sınıfların, Application'daki arayüzlere
    /// bağlandığı yer.
    ///
    /// Neden burada, Program.cs'te değil: bu kayıtlar hangi sınıfın hangi
    /// arayüzü uyguladığını bilmeyi gerektirir. O bilgi Infrastructure'a aittir.
    /// WebApi tek bir satır (AddInfrastructure) çağırır ve içerideki sınıf
    /// adlarını hiç görmez — repository'nin adı değişse WebApi'de hiçbir şey
    /// değişmez.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // PostgreSQL ayrı bir sunucu süreci; SQLite gibi "dosyayı aç, hazır"
            // değil. Uygulama ondan önce ayağa kalkabilir ya da veritabanı anlık
            // kopabilir, bu yüzden geçici bağlantı hatalarında sorgunun
            // kendiliğinden yeniden denenmesi isteniyor.
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsql => npgsql.EnableRetryOnFailure()));

            // Scoped: bir istek boyunca repository ve UnitOfWork AYNI DbContext
            // örneğini paylaşır. Repository'ye eklenen kaydı UnitOfWork'ün
            // yazabilmesi buna bağlı.
            services.AddScoped<IFileRepository, FileRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Dosya sistemine giden servis de bir altyapı detayı: Application
            // yalnızca IFolderScannerService arayüzünü tanır.
            services.AddScoped<IFolderScannerService, FolderScannerService>();

            services.Configure<FolderWatchSettings>(
                configuration.GetSection("WatchSettings"));

            return services;
        }

        /// <summary>
        /// Bekleyen migration'ları uygular.
        ///
        /// Container içinde "dotnet ef database update" çalıştırma imkânı yok
        /// (runtime imajında EF araçları bulunmaz); bu olmadan tablolar hiç
        /// oluşmaz ve ilk sorguda "relation TrackedFiles does not exist" alınır.
        /// Yerelde de zararsız: veritabanı güncelse hiçbir şey yapmaz.
        ///
        /// WebApi'nin AppDbContext'i tanımak zorunda kalmaması için uzantı
        /// olarak buraya konuldu.
        /// </summary>
        public static void ApplyMigrations(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();
        }
    }
}
