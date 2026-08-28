using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Domain.Entities;

namespace FileTrackingAndProcessingServices.Application.Interfaces
{
    /// <summary>
    /// TrackedFile'a özel sorgular. Ortak CRUD <see cref="IRepository{T}"/>'den
    /// miras alınıyor, burada yalnızca bu tabloya has olanlar tanımlı.
    ///
    /// Ayrı bir arayüz olmasının sebebi Single Responsibility: generic repository
    /// "her tabloda aynı olan"ı, bu arayüz "sadece dosyalara ait olan"ı taşır.
    /// </summary>
    public interface IFileRepository : IRepository<TrackedFile>
    {
        /// <summary>
        /// Sıralanmış ve sayfalanmış kayıtlar ile filtre uygulanmadan önceki
        /// toplam kayıt sayısı. İkisi birlikte dönüyor çünkü toplam, sayfalama
        /// uygulanmadan hesaplanmak zorunda.
        /// </summary>
        Task<(List<TrackedFile> Items, int TotalCount)> GetPagedAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uzantıya göre filtrelenmiş, sıralanmış ve sayfalanmış kayıtlar.
        ///
        /// Gelen uzantının NORMALLEŞTİRİLMİŞ olması beklenir (küçük harf, baştaki
        /// nokta eklenmiş) — normalleştirme bir kullanıcı girdisi kuralı,
        /// veritabanı işi değil, o yüzden serviste yapılıyor.
        ///
        /// Toplam sayı uzantı filtresi UYGULANDIKTAN sonra, sayfalamadan önce
        /// hesaplanır: istemcinin görmesi gereken "kaç .pdf var" sayısıdır.
        /// </summary>
        Task<(List<TrackedFile> Items, int TotalCount)> GetPagedByExtensionAsync(
            string normalizedExtension,
            FileQueryParameters parameters,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Birden fazla kayıtta geçen hash değerleri (boş hash'ler hariç),
        /// israf edilen alana göre azalan sırada ve sayfalanmış olarak; yanında
        /// toplam grup sayısı.
        ///
        /// Sıralama neden repository'de: sayfalama ancak sıra belliyken anlamlı.
        /// Sıralama bellekte yapılsaydı "en çok yer israf eden ilk 10 grup"
        /// sorgusu yanlış sayfayı döndürürdü — önce rastgele 10 grup alınır,
        /// sonra o 10'u kendi içinde sıralanmış olurdu.
        /// </summary>
        Task<(List<string> Hashes, int TotalCount)> GetDuplicateHashesPagedAsync(
            FileQueryParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>Verilen hash listesine giren tüm kayıtlar.</summary>
        Task<List<TrackedFile>> GetByHashesAsync(
            IReadOnlyCollection<string> hashes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tüm kayıtları dosya yoluna göre sözlük olarak verir. Tarayıcı, her
        /// dosya için ayrı sorgu atmamak (N+1) için bunu tek seferde çekiyor.
        ///
        /// Dönen kayıtlar DEĞİŞİKLİK TAKİBİ AÇIK gelir: tarayıcı bunların
        /// alanlarını güncelleyip SaveChanges ile yazıyor. AsNoTracking
        /// kullanılırsa güncellemeler sessizce kaybolur.
        /// </summary>
        Task<Dictionary<string, TrackedFile>> GetAllByPathAsync(CancellationToken cancellationToken = default);
    }
}
