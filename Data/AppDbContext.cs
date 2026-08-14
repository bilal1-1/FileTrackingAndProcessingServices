using Microsoft.EntityFrameworkCore;
using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<TrackedFile> TrackedFiles { get; set; }

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