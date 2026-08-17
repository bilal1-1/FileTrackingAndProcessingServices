using FileTrackingAndProcessingServices.Data;
using FileTrackingAndProcessingServices.Middleware;
using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controller desteğini ekle
builder.Services.AddControllers();

// Swagger/OpenAPI desteği
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Bizim DI kayıtlarımız
// PostgreSQL ayrı bir sunucu süreci; SQLite gibi "dosyayı aç, hazır" değil.
// Uygulama ondan önce ayağa kalkabilir ya da veritabanı anlık kopabilir, bu yüzden
// geçici bağlantı hatalarında sorgunun kendiliğinden yeniden denenmesi isteniyor.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.EnableRetryOnFailure()));

builder.Services.AddScoped<IFileTrackingService, FileTrackingService>();
builder.Services.AddScoped<IFolderScannerService, FolderScannerService>();

builder.Services.Configure<FolderWatchSettings>(
    builder.Configuration.GetSection("WatchSettings"));
builder.Services.AddHostedService<FileScanBackgroundService>();

var app = builder.Build();

// Bekleyen migration'ları uygulama açılırken çalıştır.
// Container içinde "dotnet ef database update" komutunu çalıştırma imkânı yok
// (runtime imajında EF araçları bulunmaz); bu satır olmadan tablolar hiç
// oluşmaz ve ilk sorguda relation TrackedFiles does not exist hatası alınır.
// Yerelde de zararsız: veritabanı zaten günceldeyse hiçbir şey yapmaz.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

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
