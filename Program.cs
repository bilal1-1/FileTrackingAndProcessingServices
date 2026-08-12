using FileTrackingAndProcessingServices.Data;
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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IFileTrackingService, FileTrackingService>();
builder.Services.AddScoped<IFolderScannerService, FolderScannerService>();

builder.Services.Configure<FolderWatchSettings>(
    builder.Configuration.GetSection("WatchSettings"));
builder.Services.AddHostedService<FileScanBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Controller route'larını aktif et
app.MapControllers();

app.Run();
