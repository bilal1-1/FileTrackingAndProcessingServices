using Microsoft.EntityFrameworkCore;
using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Data
{
    public class AppDbContext : DbContext
    {
        // bu constructor appsettings içinden db bilgilerini alarak veritabanı bağlantısını kuruyor.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        
        // bu da trackedfiles tablosunu temsil ediyor.
        public DbSet<TrackedFile> TrackedFiles { get; set; }

        // model nesnelerini veritabanı tablosu olarak yapılandırdığımız yer.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Yinelenen dosya tespiti Hash üzerinden gruplama yapıyor. Index olmadan
            // veritabanı her sorguda tüm tabloyu taramak zorunda kalır.
            // Benzersiz DEĞİL — aynı hash'in birden fazla satırda olması zaten
            // aradığımız durum.
            modelBuilder.Entity<TrackedFile>()
                .HasIndex(f => f.Hash);
        }
    }
}