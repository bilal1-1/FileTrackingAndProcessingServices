using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Application.Services;
using FileTrackingAndProcessingServices.Infrastructure.Persistence.Repositories;
using FileTrackingAndProcessingServices.Tests.TestHelpers;

namespace FileTrackingAndProcessingServices.Tests.Services
{
    /// <summary>
    /// FileTrackingService'in sorgulama davranışı. Her test boş bir tabloyla
    /// başlar (xUnit her test için sınıfı yeniden oluşturur ve TestDatabase
    /// tabloyu boşaltır), bu yüzden testler birbirinin verisini görmez.
    /// Sorgular gerçek bir PostgreSQL'e karşı koşar.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class FileTrackingServiceTests : IDisposable
    {
        private readonly TestDatabase _db;
        private readonly FileTrackingService _service;

        public FileTrackingServiceTests(PostgreSqlContainerFixture fixture)
        {
            _db = new TestDatabase(fixture);

            // Servis artık DbContext değil repository alıyor. Testler yine gerçek
            // repository ile koşuyor (sahte değil): amaç sorguların PostgreSQL'de
            // gerçekten çalıştığını doğrulamak.
            _service = new FileTrackingService(new FileRepository(_db.Context));
        }

        public void Dispose() => _db.Dispose();

        // Sayı adın içinde: sayfalama testleri 25 kaydın 10'luk sayfalara
        // 3 sayfa olarak bölünmesine dayanıyor, bu yüzden çağrı yerinde
        // kaç kayıt olduğu görünmeli.
        private void SeedTwentyFiveFiles()
        {
            var files = Enumerable.Range(1, 25)
                .Select(i => TestDatabase.CreateTrackedFile($"dosya-{i:00}.txt", sizeBytes: i * 10))
                .ToArray();

            _db.Seed(files);
        }

        // ---------- GetAllFilesAsync: sayfalama ----------

        [Fact]
        public async Task GetAllFiles_DefaultParameters_ReturnsFirstTen()
        {
            SeedTwentyFiveFiles();

            var result = await _service.GetAllFilesAsync(new FileQueryParameters());

            Assert.Equal(10, result.Items.Count);
            Assert.Equal(25, result.TotalCount);
            Assert.Equal(1, result.Page);
            Assert.Equal(3, result.TotalPages);
            Assert.False(result.HasPreviousPage);
            Assert.True(result.HasNextPage);
        }

        [Fact]
        public async Task GetAllFiles_SecondPage_ReturnsNextTen()
        {
            SeedTwentyFiveFiles();

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { Page = 2, PageSize = 10 });

            Assert.Equal(10, result.Items.Count);
            Assert.Equal("dosya-11.txt", result.Items.First().FileName);
            Assert.Equal("dosya-20.txt", result.Items.Last().FileName);
            Assert.True(result.HasPreviousPage);
        }

        [Fact]
        public async Task GetAllFiles_LastPage_ReturnsRemaining()
        {
            SeedTwentyFiveFiles();

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { Page = 3, PageSize = 10 });

            Assert.Equal(5, result.Items.Count);   // 25 kayıttan geriye kalan
            Assert.False(result.HasNextPage);
        }

        [Fact]
        public async Task GetAllFiles_PageBeyondEnd_ReturnsEmptyListWithCorrectTotal()
        {
            SeedTwentyFiveFiles();

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { Page = 99, PageSize = 10 });

            Assert.Empty(result.Items);
            Assert.Equal(25, result.TotalCount);   // toplam bilgisi kaybolmamalı
        }

        [Fact]
        public async Task GetAllFiles_AllPagesCombined_NoRecordRepeats()
        {
            // Sıralamada eşitlik varsa aynı kayıt iki sayfada görünebilir.
            // Servis bunu ThenBy(Id) ile engelliyor; burada aynı FileName'e sahip
            // kayıtlarla o güvence sınanıyor.
            // Dosya adları aynı ama yolları farklı — gerçek hayatta da aynı isim
            // ancak farklı klasörlerde bulunabilir. FilePath veritabanında
            // benzersiz olduğu için burada da farklı olmak zorunda.
            var files = Enumerable.Range(1, 15)
                .Select(i => TestDatabase.CreateTrackedFile(
                    "ayni-isim.txt", filePath: $@"C:\test\klasor{i}\ayni-isim.txt"))
                .ToArray();
            _db.Seed(files);

            var seenIds = new List<int>();
            for (int page = 1; page <= 3; page++)
            {
                var result = await _service.GetAllFilesAsync(new FileQueryParameters
                {
                    Page = page,
                    PageSize = 5,
                    SortBy = "fileName"
                });
                seenIds.AddRange(result.Items.Select(f => f.Id));
            }

            Assert.Equal(15, seenIds.Count);
            Assert.Equal(15, seenIds.Distinct().Count());
        }

        [Fact]
        public async Task GetAllFiles_NoRecords_ReturnsEmptyResult()
        {
            var result = await _service.GetAllFilesAsync(new FileQueryParameters());

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
            Assert.Equal(0, result.TotalPages);
        }

        // ---------- GetAllFilesAsync: sıralama ----------

        [Fact]
        public async Task GetAllFiles_SortByNameAscending_ReturnsAlphabetical()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("cccc.txt"),
                TestDatabase.CreateTrackedFile("aaaa.txt"),
                TestDatabase.CreateTrackedFile("bbbb.txt"));

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = "fileName", SortOrder = "asc" });

            Assert.Equal(
                new[] { "aaaa.txt", "bbbb.txt", "cccc.txt" },
                result.Items.Select(f => f.FileName));
        }

        [Fact]
        public async Task GetAllFiles_SortBySizeDescending_ReturnsLargestFirst()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("kucuk.txt", sizeBytes: 10),
                TestDatabase.CreateTrackedFile("buyuk.txt", sizeBytes: 5000),
                TestDatabase.CreateTrackedFile("orta.txt", sizeBytes: 300));

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = "sizeBytes", SortOrder = "desc" });

            Assert.Equal(
                new long[] { 5000, 300, 10 },
                result.Items.Select(f => f.SizeBytes));
        }

        [Theory]
        [InlineData("FILENAME")]   // büyük harf de kabul edilmeli
        [InlineData("fileName")]
        [InlineData("filename")]
        public async Task GetAllFiles_SortByIsCaseInsensitive(string sortBy)
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("bbbb.txt"),
                TestDatabase.CreateTrackedFile("aaaa.txt"));

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = sortBy });

            Assert.Equal("aaaa.txt", result.Items.First().FileName);
        }

        [Theory]
        [InlineData("bilinmeyenAlan")]
        [InlineData("")]
        [InlineData("id")]
        public async Task GetAllFiles_UnknownSortBy_FallsBackToId(string sortBy)
        {
            // Beyaz liste dışındaki değerler sorguya konmaz, Id'ye düşer.
            _db.Seed(
                TestDatabase.CreateTrackedFile("cccc.txt"),
                TestDatabase.CreateTrackedFile("aaaa.txt"),
                TestDatabase.CreateTrackedFile("bbbb.txt"));

            var result = await _service.GetAllFilesAsync(
                new FileQueryParameters { SortBy = sortBy });

            // Ekleme sırası korunur (Id artan)
            Assert.Equal(
                new[] { "cccc.txt", "aaaa.txt", "bbbb.txt" },
                result.Items.Select(f => f.FileName));
        }

        // ---------- GetByIdAsync ----------

        [Fact]
        public async Task GetById_ExistingRecord_ReturnsFileWithCorrectFields()
        {
            _db.Seed(TestDatabase.CreateTrackedFile("rapor.pdf", extension: ".pdf", sizeBytes: 2048));
            var savedFile = _db.Context.TrackedFiles.First();

            var result = await _service.GetByIdAsync(savedFile.Id);

            Assert.NotNull(result);
            Assert.Equal("rapor.pdf", result!.FileName);
            Assert.Equal(".pdf", result.Extension);
            Assert.Equal(2048, result.SizeBytes);
        }

        [Fact]
        public async Task GetById_MissingRecord_ReturnsNull()
        {
            _db.Seed(TestDatabase.CreateTrackedFile("var.txt"));

            var result = await _service.GetByIdAsync(99999);

            Assert.Null(result);
        }

        // ---------- SearchByExtensionAsync ----------

        /// <summary>
        /// Sayfalama bu testlerin konusu değil; hepsi tek sayfaya sığsın diye
        /// geniş bir sayfa boyutu veriliyor. Sayfalamanın kendisi GetAllFiles
        /// testlerinde ayrıca sınanıyor.
        /// </summary>
        private static FileQueryParameters WidePage() => new() { PageSize = 100 };

        [Fact]
        public async Task Search_MatchingExtension_ReturnsOnlyThose()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("a.txt", extension: ".txt"),
                TestDatabase.CreateTrackedFile("b.pdf", extension: ".pdf"),
                TestDatabase.CreateTrackedFile("c.txt", extension: ".txt"));

            var result = await _service.SearchByExtensionAsync(".txt", WidePage());

            Assert.Equal(2, result.Items.Count);
            Assert.All(result.Items, f => Assert.Equal(".txt", f.Extension));
        }

        [Fact]
        public async Task Search_NoMatch_ReturnsEmptyList()
        {
            _db.Seed(TestDatabase.CreateTrackedFile("a.txt", extension: ".txt"));

            var result = await _service.SearchByExtensionAsync(".docx", WidePage());

            Assert.Empty(result.Items);   // null değil, boş liste
        }

        [Theory]
        [InlineData(".TXT", ".txt")]   // diskte büyük, aranan küçük
        [InlineData(".txt", ".TXT")]   // diskte küçük, aranan büyük
        [InlineData(".TxT", ".tXt")]   // ikisi de karışık
        public async Task Search_DifferentCasing_StillMatches(
            string storedExtension, string searchTerm)
        {
            // Tarayıcı uzantıyı diskteki haliyle kaydediyor: "BELGE.TXT" -> ".TXT".
            // Kullanıcının aramada aynı harf düzenini tutturmak zorunda olmaması
            // gerekir.
            _db.Seed(TestDatabase.CreateTrackedFile("belge", extension: storedExtension));

            var result = await _service.SearchByExtensionAsync(searchTerm, WidePage());

            Assert.Single(result.Items);
        }

        [Theory]
        [InlineData(".TIF", ".tif")]
        [InlineData(".tif", ".TIF")]
        public async Task Search_ExtensionContainingLetterI_IsCultureIndependent(
            string storedExtension, string searchTerm)
        {
            // Türkçe kültürde ToLower() 'I' harfini 'ı'ya çevirir; bu da
            // veritabanındaki lower() sonucuyla ('i') uyuşmaz. Servis bu yüzden
            // aranan değeri ToLowerInvariant() ile küçültüyor. Bu test, makinenin
            // kültür ayarı ne olursa olsun eşleşmenin bozulmadığını güvenceye alır.
            _db.Seed(TestDatabase.CreateTrackedFile("resim", extension: storedExtension));

            var result = await _service.SearchByExtensionAsync(searchTerm, WidePage());

            Assert.Single(result.Items);
        }

        [Fact]
        public async Task Search_CaseInsensitivity_DoesNotReturnWrongExtensions()
        {
            // Duyarsızlık, alakasız uzantıların da gelmesi anlamına gelmemeli.
            _db.Seed(
                TestDatabase.CreateTrackedFile("a", extension: ".TXT"),
                TestDatabase.CreateTrackedFile("b", extension: ".txt"),
                TestDatabase.CreateTrackedFile("c", extension: ".pdf"),
                TestDatabase.CreateTrackedFile("d", extension: ".txtx"));

            var result = await _service.SearchByExtensionAsync(".txt", WidePage());

            Assert.Equal(2, result.Items.Count);
            Assert.All(result.Items, f => Assert.Equal(".txt", f.Extension.ToLowerInvariant()));
        }

        [Theory]
        [InlineData("pdf")]     // ödev metnindeki yazım: search?extension=pdf
        [InlineData(".pdf")]    // noktalı yazım
        [InlineData("PDF")]     // noktasız ve büyük harf
        [InlineData(" pdf ")]   // adres çubuğundan gelen boşluklar
        public async Task Search_ExtensionWithoutLeadingDot_StillMatches(string searchTerm)
        {
            // Tarayıcı uzantıyı FileInfo.Extension'dan alıyor ve o değer her zaman
            // noktayla başlıyor (".pdf"). Kullanıcı ise doğal olarak "pdf" yazar.
            // Nokta eklenmeseydi bu arama boş liste dönerdi — hata da vermeden,
            // "böyle dosya yok" görüntüsünün arkasına saklanarak.
            _db.Seed(TestDatabase.CreateTrackedFile("rapor", extension: ".pdf"));

            var result = await _service.SearchByExtensionAsync(searchTerm, WidePage());

            Assert.Single(result.Items);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Search_EmptyExtension_ReturnsEmptyList(string searchTerm)
        {
            // Boş arama çökmemeli. Hiçbir uzantı boş olmadığı için doğru cevap
            // boş listedir; servis bu durumda veritabanına hiç gitmiyor.
            _db.Seed(TestDatabase.CreateTrackedFile("rapor", extension: ".pdf"));

            var result = await _service.SearchByExtensionAsync(searchTerm, WidePage());

            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task Search_WithoutLeadingDot_DoesNotReturnWrongExtensions()
        {
            // Nokta eklemek eşleşmeyi gevşetmemeli: "pdf" araması ".pdf" bulmalı,
            // ama ".pdfx" ya da uzantısı ".pd" olanları getirmemeli.
            _db.Seed(
                TestDatabase.CreateTrackedFile("a", extension: ".pdf"),
                TestDatabase.CreateTrackedFile("b", extension: ".pdfx"),
                TestDatabase.CreateTrackedFile("c", extension: ".pd"));

            var result = await _service.SearchByExtensionAsync("pdf", WidePage());

            Assert.Single(result.Items);
            Assert.Equal(".pdf", result.Items[0].Extension);
        }

        // ---------- GetDuplicatesAsync ----------

        [Fact]
        public async Task GetDuplicates_TwoRecordsSameHash_ReturnsSingleGroup()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("kopya-a.txt", hash: "aaa", sizeBytes: 24),
                TestDatabase.CreateTrackedFile("kopya-b.txt", hash: "aaa", sizeBytes: 24));

            var result = await _service.GetDuplicatesAsync(WidePage());

            var group = Assert.Single(result.Items);
            Assert.Equal("aaa", group.Hash);
            Assert.Equal(2, group.Count);
            Assert.Equal(24, group.SizeBytes);
            Assert.Equal(24, group.WastedBytes);   // tek kopya bırakılsa 24 bayt kazanılır
            Assert.Equal(2, group.Files.Count);
        }

        [Fact]
        public async Task GetDuplicates_UniqueRecord_NotGrouped()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("kopya-a.txt", hash: "aaa"),
                TestDatabase.CreateTrackedFile("kopya-b.txt", hash: "aaa"),
                TestDatabase.CreateTrackedFile("tekil-c.txt", hash: "ccc"));

            var result = await _service.GetDuplicatesAsync(WidePage());

            var group = Assert.Single(result.Items);
            Assert.DoesNotContain(group.Files, f => f.FileName == "tekil-c.txt");
        }

        [Fact]
        public async Task GetDuplicates_ThreeCopies_CountsTwoAsWasted()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("a.txt", hash: "aaa", sizeBytes: 500),
                TestDatabase.CreateTrackedFile("b.txt", hash: "aaa", sizeBytes: 500),
                TestDatabase.CreateTrackedFile("c.txt", hash: "aaa", sizeBytes: 500));

            var result = await _service.GetDuplicatesAsync(WidePage());

            var group = Assert.Single(result.Items);
            Assert.Equal(3, group.Count);
            Assert.Equal(1000, group.WastedBytes);   // 500 * (3 - 1)
        }

        [Fact]
        public async Task GetDuplicates_EmptyHash_NotCountedAsDuplicate()
        {
            // Boş hash "henüz hesaplanmadı" demek; "içerikleri aynı" demek değil.
            _db.Seed(
                TestDatabase.CreateTrackedFile("a.txt", hash: ""),
                TestDatabase.CreateTrackedFile("b.txt", hash: ""),
                TestDatabase.CreateTrackedFile("c.txt", hash: ""));

            var result = await _service.GetDuplicatesAsync(WidePage());

            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetDuplicates_MultipleGroups_MostWastedFirst()
        {
            _db.Seed(
                // küçük grup: 10 bayt israf
                TestDatabase.CreateTrackedFile("kucuk-1.txt", hash: "kkk", sizeBytes: 10),
                TestDatabase.CreateTrackedFile("kucuk-2.txt", hash: "kkk", sizeBytes: 10),
                // büyük grup: 9000 bayt israf
                TestDatabase.CreateTrackedFile("buyuk-1.bin", hash: "bbb", sizeBytes: 9000),
                TestDatabase.CreateTrackedFile("buyuk-2.bin", hash: "bbb", sizeBytes: 9000));

            var result = await _service.GetDuplicatesAsync(WidePage());

            Assert.Equal(2, result.Items.Count);
            Assert.Equal("bbb", result.Items[0].Hash);
            Assert.Equal(9000, result.Items[0].WastedBytes);
            Assert.Equal(10, result.Items[1].WastedBytes);
        }

        [Fact]
        public async Task GetDuplicates_NoDuplicates_ReturnsEmptyList()
        {
            _db.Seed(
                TestDatabase.CreateTrackedFile("a.txt", hash: "aaa"),
                TestDatabase.CreateTrackedFile("b.txt", hash: "bbb"));

            var result = await _service.GetDuplicatesAsync(WidePage());

            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task GetDuplicates_EmptyDatabase_ReturnsEmptyList()
        {
            var result = await _service.GetDuplicatesAsync(WidePage());

            Assert.Empty(result.Items);
        }
    }
}
