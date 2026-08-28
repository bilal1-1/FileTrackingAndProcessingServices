using FileTrackingAndProcessingServices.Application.Interfaces;
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
