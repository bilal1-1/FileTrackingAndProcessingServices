using FileTrackingAndProcessingServices.Application.DTOs;
using FileTrackingAndProcessingServices.Application.Models;

namespace FileTrackingAndProcessingServices.Application.Interfaces
{
    /// <summary>
    /// Servis dışarıya entity (TrackedFile) değil DTO döner. Böylece veritabanı
    /// tablosu ile API cevabı birbirinden bağımsız değişebilir.
    /// </summary>
    public interface IFileTrackingService
    {
        // Tüm dosyaları sayfalı getirir (filtreleme/sıralama parametreleri ile)
        Task<PagedResult<TrackedFileDto>> GetAllFilesAsync(FileQueryParameters parameters);
        // ID'ye göre tek dosya getirir, bulamazsa null döner
        Task<TrackedFileDto?> GetByIdAsync(int id);
        // Uzantıya göre dosyaları arar(.pdf,.docx gibi)
        Task<List<TrackedFileDto>> SearchByExtensionAsync(string extension);
        // Hash değerine göre yinelenen dosyaları gruplar
        Task<List<DuplicateGroupDto>> GetDuplicatesAsync();
    }
}
