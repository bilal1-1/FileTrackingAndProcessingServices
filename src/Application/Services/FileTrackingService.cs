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

        public async Task<PagedResult<TrackedFileDto>> GetAllFilesAsync( // Tüm dosyaları sayfalı getirir (filtreleme/sıralama parametreleri ile)
            FileQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            var (items, totalCount) = await _repository.GetPagedAsync(parameters, cancellationToken);

            return ToPagedResult(items.ToDtoList(), parameters, totalCount);
        }

        // Verilen ID'ye sahip dosyayı getirir; bulunamazsa null döner.
        public async Task<TrackedFileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var file = await _repository.GetByIdAsync(id, cancellationToken);

            // Kayıt yoksa null dönmeye devam ediyoruz; controller bunu 404'e çeviriyor.
            return file?.ToDto();
        }

        // Verilen uzantıya (.pdf, .docx vb.) sahip dosyaları sayfalı olarak arar.
        public async Task<PagedResult<TrackedFileDto>> SearchByExtensionAsync(
            string extension, FileQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            var normalizedExtension = NormalizeExtension(extension);

            // Boş aramada veritabanına hiç gidilmiyor: hiçbir uzantı boş
            // olmadığı için sorgu zaten kesin olarak boş dönerdi.
            if (normalizedExtension.Length == 0)
            {
                return ToPagedResult(new List<TrackedFileDto>(), parameters, totalCount: 0);
            }

            var (items, totalCount) = await _repository.GetPagedByExtensionAsync(
                normalizedExtension, parameters, cancellationToken);

            return ToPagedResult(items.ToDtoList(), parameters, totalCount);
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

        public async Task<PagedResult<DuplicateGroupDto>> GetDuplicatesAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            // 1. Bu sayfaya düşen yinelenen hash'leri ve toplam grup sayısını al.
            //    Sıralama ve sayfalama veritabanında yapıldı; buraya yalnızca
            //    gösterilecek grupların hash'leri geliyor.
            var (duplicateHashes, totalCount) = await _repository
                .GetDuplicateHashesPagedAsync(parameters, cancellationToken);

            if (duplicateHashes.Count == 0)
            {
                return ToPagedResult(new List<DuplicateGroupDto>(), parameters, totalCount);
            }

            // 2. Sadece bu hash'lere ait satırları çek.
            var files = await _repository.GetByHashesAsync(duplicateHashes, cancellationToken);

            // 3. Gruplara ayır. Bu aşama bellekte — elde zaten sadece bu sayfaya
            //    düşen grupların kayıtları var, tüm tablo değil.
            var groups = files
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
                // için en işe yarar sıralama bu. Veritabanı sayfayı zaten bu
                // sıraya göre seçti; burası yalnızca sayfa İÇİNDEKİ sırayı
                // koruyor, çünkü GroupBy sırayı garanti etmez.
                .OrderByDescending(g => g.WastedBytes)
                .ThenBy(g => g.Hash)
                .ToList();

            return ToPagedResult(groups, parameters, totalCount);
        }

        /// <summary>
        /// Sayfa bilgisini cevaba iliştirir. Üç uçta da aynı olduğu için tek
        /// yerde: sayfa numarasını bir yerde parametreden, başka yerde başka
        /// kaynaktan almak sessiz tutarsızlık üretirdi.
        /// </summary>
        private static PagedResult<T> ToPagedResult<T>(
            List<T> items, FileQueryParameters parameters, int totalCount) => new()
            {
                Items = items,
                Page = parameters.Page,       // Kaçıncı sayfa
                PageSize = parameters.PageSize, // Sayfa başı kayıt
                TotalCount = totalCount       // Filtreye uyan toplam kayıt sayısı
            };
    }
}
