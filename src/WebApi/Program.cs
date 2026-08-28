using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Application.Services;
using FileTrackingAndProcessingServices.Infrastructure;
using FileTrackingAndProcessingServices.WebApi.BackgroundServices;
using FileTrackingAndProcessingServices.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Controller desteğini ekle
builder.Services.AddControllers();

// Swagger/OpenAPI desteği
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Katmanların bağlanması (composition root) ---
// Uygulamada somut sınıfların arayüzlere bağlandığı TEK yer burası. Controller
// ve servisler yalnızca arayüzleri tanır, hangi sınıfın geldiğini bilmezler.

// Infrastructure kendi kayıtlarını kendi yapıyor: DbContext, repository'ler,
// UnitOfWork, klasör tarayıcı ve WatchSettings bağlaması.
builder.Services.AddInfrastructure(builder.Configuration);

// Taranacak klasör göreli yazıldıysa mutlak yola çevrilir.
//
// Neden gerekli: göreli bir yol, uygulamanın çalışma dizinine göre çözülür ve o
// dizin nereden başlatıldığına göre değişir (IDE, terminal, "dotnet run" hepsi
// farklı davranabilir). Aynı ayar bir yerde doğru, başka yerde boş klasör
// gösterirdi — üstelik hata vermeden. İçerik köküne sabitleyince sonuç her
// durumda aynı oluyor.
//
// Ayar zaten mutlak yolsa (Docker'da /data/watch) dokunulmuyor.
// Burada duruyor çünkü içerik kökünü yalnızca sunum katmanı bilir.
builder.Services.PostConfigure<FolderWatchSettings>(settings =>
{
    if (!string.IsNullOrWhiteSpace(settings.FolderPath) && !Path.IsPathRooted(settings.FolderPath))
    {
        settings.FolderPath = Path.GetFullPath(settings.FolderPath, builder.Environment.ContentRootPath);
    }
});

// Application katmanının tek servisi. Ayrı bir uzantı metoduna gerek görülmedi;
// Application projesinde hiç NuGet paketi olmaması bilinçli bir tercih ve
// IServiceCollection için paket eklemek o tercihi bozardı.
builder.Services.AddScoped<IFileTrackingService, FileTrackingService>();

// Periyodik taramayı yürüten arka plan servisi. Uygulamanın çalışma ömrüne
// bağlı olduğu için sunum katmanında duruyor.
builder.Services.AddHostedService<FileScanBackgroundService>();

var app = builder.Build();

// Bekleyen migration'ları uygulama açılırken çalıştır.
app.Services.ApplyMigrations();

// Boru hattının en dışı: altındaki her katmanın hatasını yakalar,
// bu yüzden diğer middleware'lerden ÖNCE eklenmeli.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Controller route'larını aktif et
app.MapControllers();

app.Run();

/// <summary>
/// Üst seviye ifadelerle (top-level statements) yazılan bir Program sınıfı
/// varsayılan olarak internal üretilir ve test proje sınırının dışından
/// görülemez. WebApplicationFactory&lt;Program&gt; ise uygulamayı ayağa kaldırmak
/// için bu tipe erişmek zorunda.
///
/// Bu satır aynı sınıfı public olarak açar. Uygulamanın çalışmasına hiçbir
/// etkisi yok; tek amacı entegrasyon testlerinin gerçek boru hattını (routing,
/// middleware, DI) sahte bir kurulum yerine olduğu gibi çalıştırabilmesi.
/// </summary>
public partial class Program { }
