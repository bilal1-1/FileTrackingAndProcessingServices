using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Data;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Services
{
    public class FileTrackingService : IFileTrackingService
    {
        private readonly AppDbContext _context;

        public FileTrackingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<TrackedFile>> GetAllFilesAsync(FileQueryParameters parameters)
        {
            var query = _context.TrackedFiles.AsQueryable();

            bool descending = parameters.SortOrder
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
            // Aksi halde aynı kayıt iki farklı sayfada görünebilir.
            query = ordered.ThenBy(f => f.Id);

            // Toplam sayı, sayfalama uygulanmadan önce hesaplanır.
            int totalCount = await query.CountAsync();

            var items = await query
                .Skip((parameters.Page - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();

            return new PagedResult<TrackedFile>
            {
                Items = items,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<TrackedFile?> GetByIdAsync(int id)
        {
            return await _context.TrackedFiles.FirstOrDefaultAsync(f => f.Id == id);
        }
        
        public async Task<List<TrackedFile>> SearchByExtensionAsync(string extension)
        {
            // Uzantı karşılaştırması büyük/küçük harf duyarsız olmalı: diskte
            // "BELGE.TXT" varsa tarayıcı uzantıyı ".TXT" olarak kaydeder, kullanıcı
            // ise ".txt" arar. Duyarlı karşılaştırma bu dosyayı hiç bulamıyordu.
            //
            // İki taraf da küçük harfe indiriliyor, ama farklı metotlarla:
            // - Aranan değer C# tarafında ToLowerInvariant() ile küçültülür.
            //   ToLower() kullanılsaydı makinenin kültürü devreye girerdi; Türkçe
            //   kültürde ".TIF" -> ".tıf" olur ve veritabanındaki ".tif" ile
            //   eşleşmezdi.
            // - Kolon tarafındaki ToLower() SQL'e lower() olarak çevrilir ve
            //   karşılaştırma veritabanında yapılır; tüm tablo belleğe çekilmez.
            var aranan = extension.ToLowerInvariant();

            return await _context.TrackedFiles
                .Where(f => f.Extension.ToLower() == aranan)
                .ToListAsync();
        }

        public async Task<List<DuplicateGroup>> GetDuplicatesAsync()
        {
            // 1. Önce sadece yinelenen hash değerlerini bul. Gruplama ve sayma
            //    veritabanında yapılır (GROUP BY ... HAVING COUNT(*) > 1), satırlar
            //    belleğe çekilmez.
            //    Hash'i boş olan kayıtlar dışarıda bırakılır — henüz hesaplanmamış
            //    olmaları onları birbirinin kopyası yapmaz.
            var duplicateHashes = await _context.TrackedFiles
                .Where(f => f.Hash != "")
                .GroupBy(f => f.Hash)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToListAsync();

            if (duplicateHashes.Count == 0)
            {
                return new List<DuplicateGroup>();
            }

            // 2. Sadece bu hash'lere ait satırları çek. Tek sorgu (SQL IN), N+1 yok.
            var files = await _context.TrackedFiles
                .Where(f => duplicateHashes.Contains(f.Hash))
                .ToListAsync();

            // 3. Gruplara ayır. Bu aşama bellekte — elde zaten sadece yinelenen
            //    kayıtlar var, tüm tablo değil.
            return files
                .GroupBy(f => f.Hash)
                .Select(g => new DuplicateGroup
                {
                    Hash = g.Key,
                    SizeBytes = g.First().SizeBytes,
                    Count = g.Count(),
                    WastedBytes = g.First().SizeBytes * (g.Count() - 1),
                    Files = g.OrderBy(f => f.FilePath).ToList()
                })
                // En çok yer israf eden grup başta gelsin — listeye bakan kişi
                // için en işe yarar sıralama bu.
                .OrderByDescending(g => g.WastedBytes)
                .ThenBy(g => g.Hash)
                .ToList();
        }
    }
}