using FileTrackingAndProcessingServices.Domain.Entities;
using FileTrackingAndProcessingServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Tests.TestHelpers
{
    /// <summary>
    /// Tek bir teste ait, boş bir veritabanı ve taze bir DbContext verir.
    ///
    /// Neden sahte (mock) bir DbContext değil: servisteki sorgular LINQ olarak
    /// yazılıyor ama SQL'e çevrilerek çalışıyor. Sahte bir nesneyle test
    /// edilirse sorgular bellekte LINQ-to-Objects olarak koşar ve "SQL'e
    /// çevrilebiliyor mu" sorusu hiç sorulmamış olur.
    ///
    /// Neden SQLite değil de gerçek PostgreSQL: uygulama PostgreSQL'de
    /// çalışıyor. Aynı LINQ sorgusu her veritabanı için FARKLI SQL'e çevrilir;
    /// SQLite'a karşı koşan bir test, çalıştırılan veritabanını doğrulamış
    /// olmaz. Fark teorik de değil — PostgreSQL metni dil kurallarına göre
    /// sıralar (baştaki noktayı yok sayar, büyük/küçük harfi birincil düzeyde
    /// ayırmaz), SQLite ise bayt değerine göre sıralar.
    ///
    /// İzolasyon container yeniden başlatılarak değil, tablo boşaltılarak
    /// sağlanıyor: TRUNCATE milisaniyeler sürer, container açmak saniyeler.
    /// RESTART IDENTITY olmadan Id sayacı testler arasında büyümeye devam eder
    /// ve "ilk kaydın Id'si 1" gibi beklentiler kırılırdı.
    ///
    /// Adı "DatabaseFixture" DEĞİL: bu sınıf her test için elle oluşturuluyor,
    /// xUnit'in fixture mekanizmasıyla değil. Gerçek fixture olan
    /// PostgreSqlContainerFixture ile karışmasın.
    /// </summary>
    public sealed class TestDatabase : IDisposable
    {
        public AppDbContext Context { get; }

        public TestDatabase(PostgreSqlContainerFixture fixture)
        {
            Context = fixture.CreateContext();
            Context.Database.ExecuteSqlRaw(@"TRUNCATE TABLE ""TrackedFiles"" RESTART IDENTITY");
        }

        /// <summary>
        /// Verilen kayıtları veritabanına yazar ve değişiklik takibini temizler.
        /// Takip temizlenmezse servis, veritabanından okumak yerine bellekteki
        /// nesneleri döndürebilir ve test gerçek sorguyu doğrulamamış olur.
        ///
        /// Adı "Add" değil "Seed": sadece ekleme yapmıyor, testin başlangıç
        /// durumunu kuruyor — takip temizliği de bu işin parçası.
        /// </summary>
        public void Seed(params TrackedFile[] files)
        {
            Context.TrackedFiles.AddRange(files);
            Context.SaveChanges();
            Context.ChangeTracker.Clear();
        }

        /// <summary>
        /// Testlerde tekrar tekrar yazmamak için hazır bir TrackedFile üretir.
        /// Sadece teste konu olan alanlar dışarıdan verilir.
        ///
        /// Adı kısaca "File" DEĞİL: System.IO.File ile çakışırdı ve testlerde
        /// o tip de kullanılıyor (File.WriteAllText, File.SetLastWriteTimeUtc).
        /// </summary>
        public static TrackedFile CreateTrackedFile(
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
                // DateTimeKind.Utc şart: PostgreSQL'in timestamp with time zone
                // tipi yalnızca UTC kabul eder, Kind=Unspecified bir değer bile
                // Npgsql tarafından reddedilir.
                CreatedAt = createdAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedAt = modifiedAt ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        // Container'ı DEĞİL yalnızca bu testin bağlantısını kapatır; container
        // koleksiyondaki tüm testler bitince PostgreSqlContainerFixture
        // tarafından kapatılır.
        public void Dispose() => Context.Dispose();
    }
}
