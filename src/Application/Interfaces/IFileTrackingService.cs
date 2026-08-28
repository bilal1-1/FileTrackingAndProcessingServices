using FileTrackingAndProcessingServices.Application.DTOs;
using FileTrackingAndProcessingServices.Application.Models;

namespace FileTrackingAndProcessingServices.Application.Interfaces
{
    /// <summary>
    /// Servis dışarıya entity (TrackedFile) değil DTO döner. Böylece veritabanı
    /// tablosu ile API cevabı birbirinden bağımsız değişebilir.
    ///
    /// Liste dönen üç ucun tamamı PagedResult döner. Bazısı sayfalı bazısı
    /// sayfasız olsaydı, tablo büyüdüğünde bir uç tüm kayıtları tek cevapta
    /// dökerdi — üstelik istemci için de tutarsız bir sözleşme olurdu.
    /// </summary>
    public interface IFileTrackingService
    {
        // Tüm dosyaları sayfalı getirir (filtreleme/sıralama parametreleri ile)
        Task<PagedResult<TrackedFileDto>> GetAllFilesAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default);

        // ID'ye göre tek dosya getirir, bulamazsa null döner
        Task<TrackedFileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        // Uzantıya göre dosyaları arar (.pdf, .docx gibi), sayfalı döner
        Task<PagedResult<TrackedFileDto>> SearchByExtensionAsync(
            string extension, FileQueryParameters parameters, CancellationToken cancellationToken = default);

        // Hash değerine göre yinelenen dosyaları gruplar, sayfalı döner
        Task<PagedResult<DuplicateGroupDto>> GetDuplicatesAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default);
    }
}
