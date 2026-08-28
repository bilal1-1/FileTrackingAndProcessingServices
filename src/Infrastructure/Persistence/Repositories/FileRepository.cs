using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Domain.Entities;
using FileTrackingAndProcessingServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Infrastructure.Persistence.Repositories
{
    public class FileRepository : Repository<TrackedFile>, IFileRepository
    {
        public FileRepository(AppDbContext context) : base(context)
        {
        }

        public Task<(List<TrackedFile> Items, int TotalCount)> GetPagedAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            // AsNoTracking: bu kayıtlar yalnızca okunup DTO'ya çevrilecek, hiç
            // değiştirilmeyecek. Takip kapatılınca EF değişiklik karşılaştırması
            // için kopya tutmaz.
            return GetPagedCoreAsync(Table.AsNoTracking(), parameters, cancellationToken);
        }

        public Task<(List<TrackedFile> Items, int TotalCount)> GetPagedByExtensionAsync(
            string normalizedExtension,
            FileQueryParameters parameters,
            CancellationToken cancellationToken = default)
        {
            // Karşılaştırmanın kolon tarafındaki ToLower() SQL'e lower() olarak
            // çevrilir; karşılaştırma veritabanında yapılır, tüm tablo belleğe
            // çekilmez.
            var filtered = Table
                .AsNoTracking()
                .Where(f => f.Extension.ToLower() == normalizedExtension);

            return GetPagedCoreAsync(filtered, parameters, cancellationToken);
        }

        /// <summary>
        /// Sıralama ve sayfalama, tüm listeleme sorgularında birebir aynı.
        /// Tek yerde duruyor ki "eşitlikte Id'ye göre sırala" gibi kurallar bir
        /// sorguda uygulanıp diğerinde unutulmasın.
        /// </summary>
        private static async Task<(List<TrackedFile> Items, int TotalCount)> GetPagedCoreAsync(
            IQueryable<TrackedFile> query,
            FileQueryParameters parameters,
            CancellationToken cancellationToken)
        {
            bool descending = parameters.SortOrder // DESC veya ASC parametresi için
                .Equals("desc", StringComparison.OrdinalIgnoreCase);

            // Sıralanabilir alanlar beyaz liste ile sınırlı: istemciden gelen
            // metin doğrudan sorguya konmaz, sadece bilinen alanlara eşlenir.
            IOrderedQueryable<TrackedFile> ordered = parameters.SortBy.ToLowerInvariant() switch
            {
                "filename" => descending
                    ? query.OrderByDescending(f => f.FileName)
                    : query.OrderBy(f => f.FileName),
                "extension" => descending
                    ? query.OrderByDescending(f => f.Extension)
                    : query.OrderBy(f => f.Extension),
                "sizebytes" => descending
                    ? query.OrderByDescending(f => f.SizeBytes)
                    : query.OrderBy(f => f.SizeBytes),
                "createdat" => descending
                    ? query.OrderByDescending(f => f.CreatedAt)
                    : query.OrderBy(f => f.CreatedAt),
                "modifiedat" => descending
                    ? query.OrderByDescending(f => f.ModifiedAt)
                    : query.OrderBy(f => f.ModifiedAt),
                _ => descending
                    ? query.OrderByDescending(f => f.Id)
                    : query.OrderBy(f => f.Id)
            };

            // Eşit değerlerde sıra rastgele kalmasın diye ikincil sıralama.
            // eşit değerlerde her zaman Id'ye göre sırala, tutarlı ol.
            query = ordered.ThenBy(f => f.Id);

            // Toplam sayı, sayfalama uygulanmadan önce hesaplanır.
            int totalCount = await query.CountAsync(cancellationToken);

            var items = await query // sayfalamayı yapar
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<(List<string> Hashes, int TotalCount)> GetDuplicateHashesPagedAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            // Gruplama, sayma ve israf hesabı veritabanında yapılır
            // (GROUP BY ... HAVING COUNT(*) > 1), satırlar belleğe çekilmez.
            // Hash'i boş olan kayıtlar dışarıda bırakılır — henüz hesaplanmamış
            // olmaları onları birbirinin kopyası yapmaz.
            var groups = Table
                .AsNoTracking()
                .Where(f => f.Hash != "")      // boş hash'leri atla
                .GroupBy(f => f.Hash)          // aynı hash'e göre grupla
                .Where(g => g.Count() > 1)     // 1'den fazla olan gruplar = duplicate
                .Select(g => new
                {
                    Hash = g.Key,
                    // İçerik aynı olduğu için boyutlar da eşit; Max yalnızca
                    // gruptan tek bir değer almanın SQL'e çevrilebilen yolu.
                    WastedBytes = g.Max(f => f.SizeBytes) * (g.Count() - 1)
                });

            int totalCount = await groups.CountAsync(cancellationToken);

            // Sıralama burada, veritabanında yapılıyor. Sayfalama ancak sıra
            // belliyken anlamlı: "en çok yer israf eden ilk 10 grup" sorusunun
            // cevabı, sıralama bellekte yapılırsa yanlış sayfayı döndürürdü.
            var hashes = await groups
                .OrderByDescending(g => g.WastedBytes)
                .ThenBy(g => g.Hash)
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .Select(g => g.Hash)
                .ToListAsync(cancellationToken);

            return (hashes, totalCount);
        }

        public async Task<List<TrackedFile>> GetByHashesAsync(
            IReadOnlyCollection<string> hashes, CancellationToken cancellationToken = default)
        {
            // Tek sorgu (SQL IN), N+1 yok.
            return await Table
                .AsNoTracking()
                .Where(f => hashes.Contains(f.Hash))
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<string, TrackedFile>> GetAllByPathAsync(CancellationToken cancellationToken = default)
        {
            // Burada AsNoTracking YOK ve olmamalı: tarayıcı dönen kayıtların
            // alanlarını değiştirip kaydediyor, takip kapatılırsa değişiklikler
            // veritabanına hiç yazılmaz.
            return await Table.ToDictionaryAsync(f => f.FilePath, cancellationToken);
        }
    }
}
