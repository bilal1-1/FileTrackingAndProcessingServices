using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Services
{
    public interface IFileTrackingService
    {
        // Tüm dosyaları sayfalı getirir (filtreleme/sıralama parametreleri ile)
        Task<PagedResult<TrackedFile>> GetAllFilesAsync(FileQueryParameters parameters);
        // ID'ye göre tek dosya getirir, bulamazsa null döner
        Task<TrackedFile?> GetByIdAsync(int id);
        // Uzantıya göre dosyaları arar(.pdf,.docx gibi)
        Task<List<TrackedFile>> SearchByExtensionAsync(string extension);
        // Hash değerine göre yinelenen dosyaları gruplar   
        Task<List<DuplicateGroup>> GetDuplicatesAsync();
    }
}
