using FileTrackingAndProcessingServices.Data;
using FileTrackingAndProcessingServices.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Tests.TestHelpers
{
    /// <summary>
    /// Her test için sıfırdan, belleğe kurulmuş bir SQLite veritabanı hazırlar.
    ///
    /// Neden sahte (mock) bir DbContext değil: servisteki sorgular LINQ olarak
    /// yazılıyor ama SQL'e çevrilerek çalışıyor. Sahte bir nesneyle test edilirse
    /// sorgular bellekte LINQ-to-Objects olarak koşar ve "SQL'e çevrilebiliyor mu"
    /// sorusu hiç sorulmamış olur. Özellikle GetDuplicatesAsync'teki
    /// GROUP BY ... HAVING COUNT(*) > 1 çevirisi ancak gerçek bir veritabanında
    /// doğrulanabilir.
    ///
    /// Bellekteki veritabanı, bağlantı açık kaldığı sürece yaşar; Dispose ile
    /// bağlantı kapandığında veri de yok olur. Testler birbirini etkilemez.
    /// </summary>
    public sealed class VeritabaniOrtami : IDisposable
    {
        private readonly SqliteConnection _connection;

        public AppDbContext Context { get; }

        public VeritabaniOrtami()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            Context = new AppDbContext(options);

            // Migration'ları çalıştırmak yerine şemayı modelden kurar.
            // Testin amacı migration geçmişi değil, servis davranışı.
            Context.Database.EnsureCreated();
        }

        /// <summary>
        /// Verilen kayıtları veritabanına yazar ve değişiklik takibini temizler.
        /// Takip temizlenmezse servis, veritabanından okumak yerine bellekteki
        /// nesneleri döndürebilir ve test gerçek sorguyu doğrulamamış olur.
        /// </summary>
        public void Ekle(params TrackedFile[] kayitlar)
        {
            Context.TrackedFiles.AddRange(kayitlar);
            Context.SaveChanges();
            Context.ChangeTracker.Clear();
        }

        /// <summary>
        /// Testlerde tekrar tekrar yazmamak için hazır bir TrackedFile üretir.
        /// Sadece teste konu olan alanlar dışarıdan verilir.
        /// </summary>
        public static TrackedFile Dosya(
            string fileName,
            string extension = ".txt",
            string hash = "",
            long sizeBytes = 100,
            string? filePath = null,
            DateTime? createdAt = null,
            DateTime? modifiedAt = null)
        {
            return new TrackedFile
            {
                FileName = fileName,
                FilePath = filePath ?? $@"C:\test\{fileName}",
                Extension = extension,
                Hash = hash,
                SizeBytes = sizeBytes,
                CreatedAt = createdAt ?? new DateTime(2026, 1, 1),
                ModifiedAt = modifiedAt ?? new DateTime(2026, 1, 1)
            };
        }

        public void Dispose()
        {
            Context.Dispose();
            _connection.Dispose();
        }
    }
}
