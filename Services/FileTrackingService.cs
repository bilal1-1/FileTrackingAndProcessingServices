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
            return await _context.TrackedFiles
                .Where(f => f.Extension == extension)
                .ToListAsync();
        }
    }
}