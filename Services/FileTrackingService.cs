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

        public async Task<List<TrackedFile>> GetAllFilesAsync()
        {
            return await _context.TrackedFiles.ToListAsync();
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