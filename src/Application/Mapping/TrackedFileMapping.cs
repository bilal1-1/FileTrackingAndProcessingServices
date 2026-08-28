using FileTrackingAndProcessingServices.Application.DTOs;
using FileTrackingAndProcessingServices.Domain.Entities;

namespace FileTrackingAndProcessingServices.Application.Mapping
{
    /// <summary>
    /// Entity'den DTO'ya çeviri. Tek yerde duruyor ki dışarıya hangi alanların
    /// verildiği tek bir dosyaya bakınca görülebilsin; her serviste ayrı ayrı
    /// kopyalanmasın.
    /// </summary>
    public static class TrackedFileMapping
    {
        public static TrackedFileDto ToDto(this TrackedFile file) => new()
        {
            Id = file.Id,
            FileName = file.FileName,
            FilePath = file.FilePath,
            Extension = file.Extension,
            SizeBytes = file.SizeBytes,
            CreatedAt = file.CreatedAt,
            ModifiedAt = file.ModifiedAt
        };

        public static List<TrackedFileDto> ToDtoList(this IEnumerable<TrackedFile> files)
            => files.Select(ToDto).ToList();
    }
}
