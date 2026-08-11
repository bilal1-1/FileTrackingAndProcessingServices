using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Services
{
    public interface IFileTrackingService
    {
        Task<List<TrackedFile>> GetAllFilesAsync();
    }
}