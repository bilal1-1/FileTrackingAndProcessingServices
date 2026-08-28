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

        public async Task<(List<TrackedFile> Items, int TotalCount)> GetPagedAsync(
            FileQueryParameters parameters)
        {
            // AsNoTracking: bu kayıtlar yalnızca okunup DTO'ya çevrilecek, hiç
            // değiştirilmeyecek. Takip kapatılınca EF değişiklik karşılaştırması
            // için kopya tutmaz.
            var query = Table.AsNoTracking();

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
            int totalCount = await query.CountAsync();

            var items = await query // sayfalamayı yapar
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<TrackedFile>> GetByExtensionAsync(string normalizedExtension)
        {
            // Karşılaştırmanın kolon tarafındaki ToLower() SQL'e lower() olarak
            // çevrilir; karşılaştırma veritabanında yapılır, tüm tablo belleğe
            // çekilmez.
            return await Table
                .AsNoTracking()
                .Where(f => f.Extension.ToLower() == normalizedExtension)
                .ToListAsync();
        }

        public async Task<List<string>> GetDuplicateHashesAsync()
        {
            // Gruplama ve sayma veritabanında yapılır
            // (GROUP BY ... HAVING COUNT(*) > 1), satırlar belleğe çekilmez.
            // Hash'i boş olan kayıtlar dışarıda bırakılır — henüz hesaplanmamış
            // olmaları onları birbirinin kopyası yapmaz.
            return await Table
                .AsNoTracking()
                .Where(f => f.Hash != "")      // boş hash'leri atla
                .GroupBy(f => f.Hash)          // aynı hash'e göre grupla
                .Where(g => g.Count() > 1)     // 1'den fazla olan gruplar = duplicate
                .Select(g => g.Key)            // sadece hash değerini al (dosyaları değil)
                .ToListAsync();
        }

        public async Task<List<TrackedFile>> GetByHashesAsync(IReadOnlyCollection<string> hashes)
        {
            // Tek sorgu (SQL IN), N+1 yok.
            return await Table
                .AsNoTracking()
                .Where(f => hashes.Contains(f.Hash))
                .ToListAsync();
        }

        public async Task<Dictionary<string, TrackedFile>> GetAllByPathAsync()
        {
            // Burada AsNoTracking YOK ve olmamalı: tarayıcı dönen kayıtların
            // alanlarını değiştirip kaydediyor, takip kapatılırsa değişiklikler
            // veritabanına hiç yazılmaz.
            return await Table.ToDictionaryAsync(f => f.FilePath);
        }
    }
}
