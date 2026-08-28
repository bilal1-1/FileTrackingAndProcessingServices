using FileTrackingAndProcessingServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FileTrackingAndProcessingServices.Tests.TestHelpers
{
    /// <summary>
    /// Testlerin tamamı için TEK bir PostgreSQL container'ı ayağa kaldırır.
    ///
    /// Neden tek container: container başlatmak birkaç saniye sürer. Her test
    /// için ayrı container açılsaydı 71 test dakikalarca sürerdi. Bunun yerine
    /// container bir kez açılır, testler arası izolasyon tabloyu boşaltarak
    /// sağlanır (bkz. TestDatabase).
    ///
    /// Şema, uygulamanın açılışta yaptığının aynısı olan Database.Migrate() ile
    /// kuruluyor — EnsureCreated() ile değil. Böylece migration'ın gerçekten
    /// çalışabilir bir şema ürettiği de her test koşusunda doğrulanmış olur.
    ///
    /// Sınıf adındaki "Fixture" xUnit terimi: ICollectionFixture ile kaydedilen,
    /// ömrü tek bir testten uzun olan paylaşımlı nesne demek.
    /// </summary>
    public sealed class PostgreSqlContainerFixture : IAsyncLifetime
    {
        // İmaj sürümü sabitleniyor: docker-compose.yml de postgres:17 kullanıyor.
        // Testlerin üretimden farklı bir sürüme koşması, bu geçişte kaçınmak
        // istediğimiz "test edilen şey çalıştırılan şey değil" durumudur.
        private readonly PostgreSqlContainer _container =
            new PostgreSqlBuilder("postgres:17").Build();

        /// <summary>
        /// Container ayağa kalktıktan sonra dolan bağlantı dizesi. Port her
        /// koşuda rastgele seçildiği için sabit yazılamaz, container'dan alınır.
        /// </summary>
        public string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            // Şemayı bir kez kur. Testler tabloyu boşaltarak izole olduğu için
            // her testte yeniden kurmaya gerek yok.
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        /// <summary>
        /// Bu container'a bağlı yeni bir DbContext üretir.
        /// </summary>
        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            return new AppDbContext(options);
        }

        public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    }

    /// <summary>
    /// Veritabanı kullanan test sınıflarını tek bir koleksiyonda toplar.
    ///
    /// İki işi birden yapıyor: (1) container'ı sınıflar arasında paylaştırır,
    /// (2) aynı koleksiyondaki sınıfların PARALEL koşmasını engeller. İkincisi
    /// şart — testler tek bir veritabanını paylaşıp tabloyu boşaltarak izole
    /// oluyor, paralel koşsalardı birbirlerinin verisini silerlerdi.
    /// </summary>
    [CollectionDefinition(Name)]
    public class DatabaseCollection : ICollectionFixture<PostgreSqlContainerFixture>
    {
        public const string Name = "PostgreSQL";

        // Gövde bilinçli olarak boş: xUnit bu sınıfı yalnızca yukarıdaki
        // öznitelik ve arayüzü okumak için kullanır, örneğini hiç oluşturmaz.
    }
}
