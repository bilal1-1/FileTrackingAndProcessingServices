using FileTrackingAndProcessingServices.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Infrastructure.Persistence
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

            // Bir dosya yolu tabloda yalnızca BİR kez bulunabilir.
            //
            // Tekrar kontrolü tarayıcının içinde de var (kayıtlı yollar sözlüğe
            // çekilip bakılıyor), ama o kontrol ile ekleme arasında zaman geçiyor.
            // Aynı anda iki tarama koşarsa ikisi de "bu dosya yok" görüp aynı
            // satırı ekleyebilir. Uygulama seviyesindeki kontrol bunu daraltır,
            // ortadan kaldırmaz — son sözü veritabanı söylemeli.
            //
            // Benzersiz OLMAYAN Hash index'iyle karıştırılmamalı: aynı hash'in
            // birden fazla satırda olması zaten aradığımız durum (yinelenen
            // dosyalar), aynı yolun iki kez olması ise her zaman hatadır.
            modelBuilder.Entity<TrackedFile>()
                .HasIndex(f => f.FilePath)
                .IsUnique();
        }
    }
}
