using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Services
{
    public interface IFileTrackingService
    {
        Task<List<TrackedFile>> GetAllFilesAsync();
        Task<TrackedFile?> GetByIdAsync(int id);
        Task<List<TrackedFile>> SearchByExtensionAsync(string extension);
    }
}
