using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Repositories;
using FileTrackingAndProcessingServices.Services;
using FileTrackingAndProcessingServices.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FileTrackingAndProcessingServices.Tests.Services
{
    /// <summary>
    /// FolderScannerService'in tarama davranışı: yeni dosya algılama, tekrar
    /// kontrolü ve hash karşılaştırması. Gerçek bir geçici klasör ve gerçek bir
    /// PostgreSQL kullanılıyor.
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public class FolderScannerServiceTests : IDisposable
    {
        private readonly TestDatabase _db;
        private readonly TempFolder _folder;

        public FolderScannerServiceTests(PostgreSqlContainerFixture fixture)
        {
            _db = new TestDatabase(fixture);
            _folder = new TempFolder();
        }

        public void Dispose()
        {
            _db.Dispose();
            _folder.Dispose();
        }

        // Parametre verilmezse testin kendi geçici klasörünü tarar; verilirse
        // (ör. var olmayan bir yol) onu tarar.
        private FolderScannerService CreateScanner(string? folderPath = null)
        {
            var settings = Options.Create(new FolderWatchSettings
            {
                FolderPath = folderPath ?? _folder.FullPath,
                ScanIntervalSeconds = 60
            });

            // Repository ve UnitOfWork AYNI DbContext'i almalı: tarayıcı
            // repository üzerinden ekliyor, UnitOfWork üzerinden kaydediyor.
            // Farklı context verilirse eklenen kayıtlar hiç yazılmaz.
            return new FolderScannerService(
                new FileRepository(_db.Context),
                new UnitOfWork(_db.Context),
                settings,
                NullLogger<FolderScannerService>.Instance);
        }

        // ---------- Yeni dosya algılama ----------

        [Fact]
        public async Task Scan_NewFile_IsSavedWithAllFieldsPopulated()
        {
            _folder.WriteFile("rapor.txt", "merhaba dunya");

            int newFileCount = await CreateScanner().ScanFolderAsync();

            Assert.Equal(1, newFileCount);

            var file = await _db.Context.TrackedFiles.SingleAsync();
            Assert.Equal("rapor.txt", file.FileName);
            Assert.Equal(".txt", file.Extension);
            Assert.Equal(Path.Combine(_folder.FullPath, "rapor.txt"), file.FilePath);
            Assert.Equal(13, file.SizeBytes);              // "merhaba dunya" 13 bayt
            Assert.Equal(64, file.Hash.Length);            // SHA-256 hex karşılığı
        }

        [Fact]
        public async Task Scan_EmptyFolder_ReturnsZero()
        {
            int newFileCount = await CreateScanner().ScanFolderAsync();

            Assert.Equal(0, newFileCount);
            Assert.Empty(_db.Context.TrackedFiles);
        }

        [Fact]
        public async Task Scan_MissingFolder_ReturnsZeroWithoutThrowing()
        {
            var missingFolderPath = Path.Combine(Path.GetTempPath(), "boyle-bir-klasor-yok-" + Guid.NewGuid());

            int newFileCount = await CreateScanner(missingFolderPath).ScanFolderAsync();

            Assert.Equal(0, newFileCount);
            Assert.Empty(_db.Context.TrackedFiles);
        }

        [Fact]
        public async Task Scan_FileInSubFolder_IsNotScannedYet()
        {
            // GetFiles() alt klasörlere inmiyor. Bu test mevcut davranışı
            // kayıt altına alıyor; alt klasör desteği eklenirse burası da
            // değişmeli.
            var subFolder = _folder.CreateSubFolder("arsiv");
            File.WriteAllText(Path.Combine(subFolder, "gizli.txt"), "icerik");
            _folder.WriteFile("gorunur.txt", "icerik");

            int newFileCount = await CreateScanner().ScanFolderAsync();

            Assert.Equal(1, newFileCount);
            var file = await _db.Context.TrackedFiles.SingleAsync();
            Assert.Equal("gorunur.txt", file.FileName);
        }

        // ---------- Tekrar kontrolü ----------

        [Fact]
        public async Task Scan_SameFileTwice_DoesNotCreateSecondRecord()
        {
            _folder.WriteFile("rapor.txt", "icerik");

            int firstScan = await CreateScanner().ScanFolderAsync();
            int secondScan = await CreateScanner().ScanFolderAsync();

            Assert.Equal(1, firstScan);
            Assert.Equal(0, secondScan);      // tekrar işlenmemeli
            Assert.Equal(1, await _db.Context.TrackedFiles.CountAsync());
        }

        [Fact]
        public async Task Scan_ThreeTimesInARow_RecordCountStaysConstant()
        {
            _folder.WriteFile("a.txt", "aaa");
            _folder.WriteFile("b.txt", "bbb");

            await CreateScanner().ScanFolderAsync();
            await CreateScanner().ScanFolderAsync();
            await CreateScanner().ScanFolderAsync();

            Assert.Equal(2, await _db.Context.TrackedFiles.CountAsync());
        }

        // ---------- Hash davranışı ----------

        [Fact]
        public async Task Scan_ComputedHash_MatchesKnownSha256Value()
        {
            // Dışarıdan doğrulama: "hello" metninin SHA-256 karşılığı bilinen
            // sabit bir değer. Servisin ürettiği hash buna eşit değilse
            // implementasyon yanlıştır.
            _folder.WriteFile("hello.txt", "hello");

            await CreateScanner().ScanFolderAsync();

            var file = await _db.Context.TrackedFiles.SingleAsync();
            Assert.Equal(
                "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
                file.Hash);
        }

        [Fact]
        public async Task Scan_DifferentFilesSameContent_GetSameHash()
        {
            _folder.WriteFile("kopya-a.txt", "birebir ayni icerik");
            _folder.WriteFile("kopya-b.txt", "birebir ayni icerik");

            await CreateScanner().ScanFolderAsync();

            var files = await _db.Context.TrackedFiles.ToListAsync();
            Assert.Equal(2, files.Count);
            Assert.Equal(files[0].Hash, files[1].Hash);
        }

        [Fact]
        public async Task Scan_DifferentContent_GetsDifferentHash()
        {
            _folder.WriteFile("a.txt", "birinci icerik");
            _folder.WriteFile("b.txt", "ikinci icerik");

            await CreateScanner().ScanFolderAsync();

            var files = await _db.Context.TrackedFiles.ToListAsync();
            Assert.NotEqual(files[0].Hash, files[1].Hash);
        }

        [Fact]
        public async Task Scan_ContentChanged_HashRefreshedWithoutNewRow()
        {
            var filePath = _folder.WriteFile("rapor.txt", "eski icerik");
            await CreateScanner().ScanFolderAsync();

            // "before/after" adlandırması bilinçli: bu ikisi farklı kayıtlar
            // değil, AYNI satırın tarama öncesi ve sonrası hali.
            var beforeScan = await _db.Context.TrackedFiles.SingleAsync();
            var originalHash = beforeScan.Hash;
            var originalId = beforeScan.Id;
            _db.Context.ChangeTracker.Clear();

            File.WriteAllText(filePath, "tamamen farkli yeni icerik");
            int newFileCount = await CreateScanner().ScanFolderAsync();

            Assert.Equal(0, newFileCount);   // yeni dosya değil, güncelleme
            var afterScan = await _db.Context.TrackedFiles.SingleAsync();
            Assert.Equal(originalId, afterScan.Id);            // aynı satır
            Assert.NotEqual(originalHash, afterScan.Hash);     // hash yenilendi
            Assert.Equal(26, afterScan.SizeBytes);             // boyut da tazelendi
        }

        [Fact]
        public async Task Scan_OnlyTimestampChanged_HashStaysSame()
        {
            // Yedekten geri yükleme / dosyanın değiştirilmeden kaydedilmesi
            // senaryosu: tarih değişir, içerik aynı kalır. Hash yeniden
            // hesaplanır ama sonuç değişmemelidir.
            var filePath = _folder.WriteFile("rapor.txt", "degismeyen icerik");
            await CreateScanner().ScanFolderAsync();

            var originalHash = (await _db.Context.TrackedFiles.SingleAsync()).Hash;
            _db.Context.ChangeTracker.Clear();

            // Tarayıcı diske UTC yazıyor; test de UTC uçlarını kullanmalı, aksi
            // halde karşılaştırma saat dilimi farkı kadar kayar.
            var shiftedTimestamp = File.GetLastWriteTimeUtc(filePath).AddHours(5);
            File.SetLastWriteTimeUtc(filePath, shiftedTimestamp);

            await CreateScanner().ScanFolderAsync();

            var afterScan = await _db.Context.TrackedFiles.SingleAsync();
            Assert.Equal(originalHash, afterScan.Hash);            // içerik aynı → hash aynı
            Assert.Equal(shiftedTimestamp, afterScan.ModifiedAt);  // tarih tazelendi
        }

        [Fact]
        public async Task Scan_LegacyRecordWithEmptyHash_IsBackfilled()
        {
            // Hash alanı eklenmeden önce oluşmuş kayıtlar geri doldurulmalı;
            // veritabanını sıfırlamaya gerek kalmamalı.
            var filePath = _folder.WriteFile("eski.txt", "icerik");
            var fileInfo = new FileInfo(filePath);

            _db.Seed(new TrackedFile
            {
                FileName = fileInfo.Name,
                FilePath = fileInfo.FullName,
                Extension = fileInfo.Extension,
                Hash = "",                                  // hash'siz eski kayıt
                SizeBytes = fileInfo.Length,
                CreatedAt = fileInfo.CreationTimeUtc,
                ModifiedAt = fileInfo.LastWriteTimeUtc
            });

            int newFileCount = await CreateScanner().ScanFolderAsync();

            Assert.Equal(0, newFileCount);                  // yeni kayıt açılmadı
            var file = await _db.Context.TrackedFiles.SingleAsync();
            Assert.Equal(64, file.Hash.Length);             // hash dolduruldu
        }

        // ---------- Uçtan uca: tarama + yinelenen tespiti ----------

        [Fact]
        public async Task AfterScan_DuplicateDetection_FindsCopies()
        {
            _folder.WriteFile("kopya-a.txt", "ayni icerik");
            _folder.WriteFile("kopya-b.txt", "ayni icerik");
            _folder.WriteFile("tekil-c.txt", "farkli icerik");

            await CreateScanner().ScanFolderAsync();

            var trackingService = new FileTrackingService(new FileRepository(_db.Context));
            var groups = await trackingService.GetDuplicatesAsync();

            var group = Assert.Single(groups);
            Assert.Equal(2, group.Count);
            Assert.DoesNotContain(group.Files, f => f.FileName == "tekil-c.txt");
        }
    }
}
