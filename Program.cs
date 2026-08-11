using FileTrackingAndProcessingServices.Data;
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Controller route'larını aktif et
app.MapControllers();

app.Run();
