using System.Net;
using System.Text.Json;
using FileTrackingAndProcessingServices.Application.DTOs;
using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileTrackingAndProcessingServices.Tests.Api
{
    /// <summary>
    /// HTTP seviyesindeki davranış: durum kodları, rota eşleşmesi, cevap zarfı
    /// ve hata middleware'i.
    ///
    /// Diğer test sınıfları servisleri doğrudan çağırıyor; burada istek gerçek
    /// boru hattından geçiyor. İkisi farklı soruları cevaplıyor: servis testleri
    /// "sorgu doğru sonucu veriyor mu", bu testler "dışarıdan bakan biri doğru
    /// cevabı alıyor mu" diye soruyor. Bir kayıt bulunamadığında servisin null
    /// dönmesi ile istemcinin 404 alması ayrı iki şey.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public sealed class FilesEndpointsTests : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions =
            new() { PropertyNameCaseInsensitive = true };

        private readonly TestDatabase _db;
        private readonly TempFolder _watchFolder;
        private readonly ApiFactory _factory;
        private readonly HttpClient _client;

        public FilesEndpointsTests(PostgreSqlContainerFixture fixture)
        {
            _db = new TestDatabase(fixture);
            _watchFolder = new TempFolder();
            _factory = new ApiFactory(fixture.ConnectionString, _watchFolder.FullPath);
            _client = _factory.CreateClient();
        }

        public void Dispose()
        {
            _client.Dispose();
            _factory.Dispose();
            _watchFolder.Dispose();
            _db.Dispose();
        }

        // ---------- GET /api/files ----------

        [Fact]
        public async Task GetAll_ReturnsPagedEnvelope()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("a.txt"),
                TestDatabase.CreateTrackedFile("b.txt"));

            var response = await _client.GetAsync("/api/files");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await ReadAsync<PagedResult<TrackedFileDto>>(response);
            Assert.Equal(2, page.Items.Count);
            Assert.Equal(2, page.TotalCount);
            Assert.Equal(1, page.Page);
        }

        // ---------- GET /api/files/{id} ----------

        [Fact]
        public async Task GetById_ExistingRecord_Returns200()
        {
            _db.Seed(TestDatabase.CreateTrackedFile("rapor.pdf", extension: ".pdf"));
            var id = _db.Context.TrackedFiles.Single().Id;

            var response = await _client.GetAsync($"/api/files/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var file = await ReadAsync<TrackedFileDto>(response);
            Assert.Equal("rapor.pdf", file.FileName);
        }

        [Fact]
        public async Task GetById_MissingRecord_Returns404()
        {
            // Servis bu durumda null döner; onun 404'e çevrildiğini yalnızca
            // buradan bakınca görebiliyoruz.
            var response = await _client.GetAsync("/api/files/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ---------- GET /api/files/search ----------

        [Fact]
        public async Task Search_WithoutExtension_Returns400()
        {
            // Eksik parametre istemci hatasıdır. Bu kontrol olmadan servis
            // null'a takılıp 500 dönüyordu — yani "sunucu bozuldu" diyordu,
            // oysa hatalı olan istekti.
            var response = await _client.GetAsync("/api/files/search");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Search_MatchingExtension_Returns200WithPagedEnvelope()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("a.txt", extension: ".txt"),
                TestDatabase.CreateTrackedFile("b.pdf", extension: ".pdf"));

            // Noktasız yazım bilinçli: adres çubuğundan gelen doğal yazım bu.
            var response = await _client.GetAsync("/api/files/search?extension=txt");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await ReadAsync<PagedResult<TrackedFileDto>>(response);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(".txt", Assert.Single(page.Items).Extension);
        }

        [Fact]
        public async Task Search_RespectsPageSize()
        {
            _db.Seed(Enumerable.Range(1, 5)
                .Select(i => TestDatabase.CreateTrackedFile($"dosya-{i}.txt", extension: ".txt"))
                .ToArray());

            var response = await _client.GetAsync("/api/files/search?extension=txt&pageSize=2");

            var page = await ReadAsync<PagedResult<TrackedFileDto>>(response);
            Assert.Equal(2, page.Items.Count);   // sayfa boyutu
            Assert.Equal(5, page.TotalCount);    // filtreye uyan toplam
            Assert.True(page.HasNextPage);
        }

        // ---------- GET /api/files/duplicates ----------

        [Fact]
        public async Task Duplicates_MatchesBeforeIdRoute()
        {
            // "duplicates" bir sayı değil; rota önceliği yanlış olsaydı istek
            // "{id}" kalıbına düşer ve 404 dönerdi. Bu davranış kodda yorumla
            // açıklanmıştı ama sınanmıyordu.
            _db.Seed(
                TestDatabase.CreateTrackedFile("kopya-a.txt", hash: "aaa", sizeBytes: 24),
                TestDatabase.CreateTrackedFile("kopya-b.txt", hash: "aaa", sizeBytes: 24));

            var response = await _client.GetAsync("/api/files/duplicates");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await ReadAsync<PagedResult<DuplicateGroupDto>>(response);
            var group = Assert.Single(page.Items);
            Assert.Equal("aaa", group.Hash);
            Assert.Equal(2, group.Count);
        }

        [Fact]
        public async Task Duplicates_EmptyDatabase_Returns200WithEmptyItems()
        {
            var response = await _client.GetAsync("/api/files/duplicates");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var page = await ReadAsync<PagedResult<DuplicateGroupDto>>(response);
            Assert.Empty(page.Items);
            Assert.Equal(0, page.TotalCount);
        }

        // ---------- Hata middleware'i ----------

        [Fact]
        public async Task UnhandledException_Returns500WithTraceIdAndNoStackTrace()
        {
            // Servis yerine her çağrıda patlayan bir uygulama konuluyor. Amaç
            // servisi test etmek değil, hatanın boru hattında nasıl karşılandığını
            // görmek: istemciye stack trace sızmamalı, durum 500 olmalı ve cevapta
            // sunucu loglarıyla eşleştirilebilecek bir traceId bulunmalı.
            using var factory = _factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IFileTrackingService>();
                    services.AddScoped<IFileTrackingService, ThrowingFileTrackingService>();
                }));

            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/files");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);

            Assert.False(string.IsNullOrWhiteSpace(
                json.RootElement.GetProperty("traceId").GetString()));

            // İçerideki hata mesajı ve yığın izi dışarı çıkmamalı.
            Assert.DoesNotContain("ThrowingFileTrackingService", body);
            Assert.DoesNotContain("at ", body);
        }

        private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            var value = JsonSerializer.Deserialize<T>(body, JsonOptions);

            Assert.NotNull(value);
            return value!;
        }

        /// <summary>
        /// Yalnızca hata yolunu tetiklemek için var; her metodu hata fırlatır.
        /// </summary>
        private sealed class ThrowingFileTrackingService : IFileTrackingService
        {
            private static Exception Boom() => new InvalidOperationException("test amaçlı hata");

            public Task<PagedResult<TrackedFileDto>> GetAllFilesAsync(
                FileQueryParameters parameters, CancellationToken cancellationToken = default)
                => throw Boom();

            public Task<TrackedFileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
                => throw Boom();

            public Task<PagedResult<TrackedFileDto>> SearchByExtensionAsync(
                string extension, FileQueryParameters parameters, CancellationToken cancellationToken = default)
                => throw Boom();

            public Task<PagedResult<DuplicateGroupDto>> GetDuplicatesAsync(
                FileQueryParameters parameters, CancellationToken cancellationToken = default)
                => throw Boom();
        }
    }
}
