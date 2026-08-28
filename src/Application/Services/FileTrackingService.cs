using FileTrackingAndProcessingServices.Application.DTOs;
using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Mapping;
using FileTrackingAndProcessingServices.Application.Models;

namespace FileTrackingAndProcessingServices.Application.Services
{
    /// <summary>
    /// Sorgulama uçlarının iş katmanı. Artık DbContext'i tanımıyor: veritabanına
    /// nasıl gidileceği repository'nin, dışarıya ne verileceği bu sınıfın işi.
    /// </summary>
    public class FileTrackingService : IFileTrackingService
    {
        private readonly IFileRepository _repository;

        public FileTrackingService(IFileRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<TrackedFileDto>> GetAllFilesAsync(FileQueryParameters parameters) // Tüm dosyaları sayfalı getirir (filtreleme/sıralama parametreleri ile)
        {
            var (items, totalCount) = await _repository.GetPagedAsync(parameters);

            return new PagedResult<TrackedFileDto>
            {
                Items = items.ToDtoList(), // Bu sayfadaki dosyalar (entity değil, DTO)
                Page = parameters.Page, // Kaçıncı sayfa
                PageSize = parameters.PageSize, // Sayfa başı kayıt
                TotalCount = totalCount // Toplam dosya sayısı
            };
        }

        // Verilen ID'ye sahip dosyayı getirir; bulunamazsa null döner.
        public async Task<TrackedFileDto?> GetByIdAsync(int id)
        {
            var file = await _repository.GetByIdAsync(id);

            // Kayıt yoksa null dönmeye devam ediyoruz; controller bunu 404'e çeviriyor.
            return file?.ToDto();
        }

        // Verilen uzantıya (.pdf, .docx vb.) sahip tüm dosyaları arar.
        public async Task<List<TrackedFileDto>> SearchByExtensionAsync(string extension)
        {
            var normalizedExtension = NormalizeExtension(extension);

            // Boş aramada veritabanına hiç gidilmiyor: hiçbir uzantı boş
            // olmadığı için sorgu zaten kesin olarak boş dönerdi.
            if (normalizedExtension.Length == 0)
            {
                return new List<TrackedFileDto>();
            }

            var files = await _repository.GetByExtensionAsync(normalizedExtension);

            return files.ToDtoList();
        }

        /// <summary>
        /// Kullanıcının yazdığı uzantıyı, veritabanında saklanan biçime çevirir.
        ///
        /// Repository'ye değil serviste duruyor: bu bir kullanıcı girdisi kuralı,
        /// veritabanı erişimiyle ilgisi yok. Repository normalleştirilmiş değeri
        /// hazır bekliyor.
        /// </summary>
        private static string NormalizeExtension(string extension)
        {
            // Küçük harfe indirme ToLowerInvariant() ile yapılıyor, ToLower() ile
            // DEĞİL: ToLower() makinenin kültürünü kullanır ve Türkçe kültürde
            // "I" harfinin küçüğü "ı"dır. ".TIF" -> ".tıf" olur, veritabanındaki
            // kültür tanımayan lower() ise ".tif" üretir; noktalı ı ile noktasız i
            // eşleşmez ve arama SADECE Türkçe makinelerde sessizce boş dönerdi.
            //
            // Büyük/küçük harf duyarsızlığı şart, çünkü diskte "BELGE.TXT" varsa
            // tarayıcı uzantıyı ".TXT" olarak kaydeder ama kullanıcı ".txt" arar.
            var normalized = (extension ?? string.Empty).Trim().ToLowerInvariant();

            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            // Baştaki nokta yoksa ekleniyor. Tarayıcı uzantıyı FileInfo.Extension
            // üzerinden alıyor ve o değer HER ZAMAN noktayla başlıyor (".pdf").
            // Kullanıcı ise doğal olarak "pdf" yazar — ödev metnindeki örnek de
            // (search?extension=pdf) noktasız. Bu satır olmadan "pdf" araması
            // hiçbir şey bulamıyor ve üstelik hata da vermiyordu: sonuç boş liste
            // olduğu için "böyle dosya yok" gibi görünüyordu.
            return normalized.StartsWith('.') ? normalized : '.' + normalized;
        }

        public async Task<List<DuplicateGroupDto>> GetDuplicatesAsync()
        {
            // 1. Önce sadece yinelenen hash değerlerini bul.
            var duplicateHashes = await _repository.GetDuplicateHashesAsync();

            if (duplicateHashes.Count == 0)
            {
                return new List<DuplicateGroupDto>();
            }

            // 2. Sadece bu hash'lere ait satırları çek.
            var files = await _repository.GetByHashesAsync(duplicateHashes);

            // 3. Gruplara ayır. Bu aşama bellekte — elde zaten sadece yinelenen
            //    kayıtlar var, tüm tablo değil.
            return files
                .GroupBy(f => f.Hash)
                .Select(g => new DuplicateGroupDto
                {
                    Hash = g.Key,
                    SizeBytes = g.First().SizeBytes,
                    Count = g.Count(),
                    WastedBytes = g.First().SizeBytes * (g.Count() - 1),
                    Files = g.OrderBy(f => f.FilePath).ToDtoList()
                })
                // En çok yer israf eden grup başta gelsin — listeye bakan kişi
                // için en işe yarar sıralama bu.
                .OrderByDescending(g => g.WastedBytes)
                .ThenBy(g => g.Hash)
                .ToList();
        }
    }
}
