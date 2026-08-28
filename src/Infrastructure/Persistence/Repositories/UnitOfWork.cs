using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Infrastructure.Persistence;

namespace FileTrackingAndProcessingServices.Infrastructure.Persistence.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        // Repository'ler ve UnitOfWork DI'da Scoped kayıtlı; bir istek boyunca
        // hepsi AYNI DbContext örneğini alır. Bu yüzden repository üzerinden
        // eklenen kayıt, buradaki SaveChanges ile yazılabiliyor.
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
