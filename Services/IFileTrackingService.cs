using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Services
{
    public interface IFileTrackingService
    {
        Task<PagedResult<TrackedFile>> GetAllFilesAsync(FileQueryParameters parameters);
        Task<TrackedFile?> GetByIdAsync(int id);
        Task<List<TrackedFile>> SearchByExtensionAsync(string extension);
        Task<List<DuplicateGroup>> GetDuplicatesAsync();
    }
}
