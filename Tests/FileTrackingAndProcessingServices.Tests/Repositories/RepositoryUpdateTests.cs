using FileTrackingAndProcessingServices.Domain.Entities;
using FileTrackingAndProcessingServices.Infrastructure.Persistence.Repositories;
using FileTrackingAndProcessingServices.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Tests.Repositories
{
    /// <summary>
    /// Generic repository'nin <c>Update</c> metodu.
    ///
    /// Bu metot eklenmeden önce güncellemeler yalnızca EF Core'un değişiklik
    /// takibi sayesinde yazılıyordu. Sorun şuydu: bu repository'deki diğer TÜM
    /// okuma metotları AsNoTracking kullanıyor. Aynı şey güncellenen kayıtları
    /// getiren sorguya da eklenirse, alan değiştirmek hiçbir şey yapmaz —
    /// hata vermez, test patlamaz, veri sessizce kaydedilmez.
    ///
    /// Aşağıdaki test tam olarak o senaryoyu kuruyor: takip KAPALI bir sorgudan
    /// gelen kayıt üzerinde Update çağrılıyor ve değişikliğin yine de yazıldığı
    /// doğrulanıyor. Yani burada sınanan şey "güncelleme çalışıyor mu" değil,
    /// "güncelleme takip davranışından bağımsız mı".
    /// </summary>
    [Collection(DatabaseCollection.Name)]
    public sealed class RepositoryUpdateTests : IDisposable
    {
        private readonly TestDatabase _db;
        private readonly FileRepository _repository;
        private readonly UnitOfWork _unitOfWork;

        public RepositoryUpdateTests(PostgreSqlContainerFixture fixture)
        {
            _db = new TestDatabase(fixture);
            _repository = new FileRepository(_db.Context);
            _unitOfWork = new UnitOfWork(_db.Context);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task Update_EntityFromUntrackedQuery_ChangeIsPersisted()
        {
            _db.Seed(TestDatabase.CreateTrackedFile("rapor.txt", hash: "eski", sizeBytes: 10));

            // AsNoTracking: kayıt EF tarafından izlenmiyor. Update olmadan
            // aşağıdaki değişiklikler SaveChanges'te hiç görülmezdi.
            var untracked = await _db.Context.TrackedFiles
                .AsNoTracking()
                .SingleAsync();

            untracked.Hash = "yeni";
            untracked.SizeBytes = 99;

            _repository.Update(untracked);
            await _unitOfWork.SaveChangesAsync();

            // Bellekteki nesneyi değil veritabanındaki satırı okuyalım.
            _db.Context.ChangeTracker.Clear();
            var reloaded = await _db.Context.TrackedFiles.SingleAsync();

            Assert.Equal("yeni", reloaded.Hash);
            Assert.Equal(99, reloaded.SizeBytes);
        }

        [Fact]
        public async Task Update_WithoutSaveChanges_NothingIsWritten()
        {
            // Repository metotları kendi başlarına kaydetmez; yazma anını
            // UnitOfWork belirler. Update de bu kurala uyar.
            _db.Seed(TestDatabase.CreateTrackedFile("rapor.txt", hash: "eski"));

            var untracked = await _db.Context.TrackedFiles.AsNoTracking().SingleAsync();
            untracked.Hash = "yeni";

            _repository.Update(untracked);
            // SaveChangesAsync BİLEREK çağrılmıyor.

            _db.Context.ChangeTracker.Clear();
            var reloaded = await _db.Context.TrackedFiles.SingleAsync();

            Assert.Equal("eski", reloaded.Hash);
        }

        [Fact]
        public async Task Update_TrackedEntity_StillWorks()
        {
            // Takip açıkken de çağrılabilmeli: aynı kaydı iki kez izlemeye
            // çalışmak EF'te hata verebilir, Update bu durumu doğru ele almalı.
            // Tarayıcı gerçekte bu yoldan geçiyor (GetAllByPathAsync takip açık).
            _db.Seed(TestDatabase.CreateTrackedFile("rapor.txt", hash: "eski"));

            var tracked = await _db.Context.TrackedFiles.SingleAsync();
            tracked.Hash = "yeni";

            _repository.Update(tracked);
            await _unitOfWork.SaveChangesAsync();

            _db.Context.ChangeTracker.Clear();
            var reloaded = await _db.Context.TrackedFiles.SingleAsync();

            Assert.Equal("yeni", reloaded.Hash);
        }

        [Fact]
        public async Task Add_DuplicateFilePath_IsRejectedByDatabase()
        {
            // Aynı yolun iki kez kaydedilmesi her zaman hatadır. Tarayıcıdaki
            // tekrar kontrolü bunu daraltır ama iki tarama aynı anda koşarsa
            // ikisi de "bu dosya yok" görebilir. Son sözü veritabanı söylemeli.
            _db.Seed(TestDatabase.CreateTrackedFile("rapor.txt", filePath: @"C:\test\rapor.txt"));

            await _repository.AddAsync(new TrackedFile
            {
                FileName = "rapor.txt",
                FilePath = @"C:\test\rapor.txt",   // aynı yol
                Extension = ".txt",
                Hash = "farkli",
                SizeBytes = 1,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ModifiedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => _unitOfWork.SaveChangesAsync());
        }
    }
}
