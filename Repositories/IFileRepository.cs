using FileTrackingAndProcessingServices.Models;

namespace FileTrackingAndProcessingServices.Repositories
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
        Task<(List<TrackedFile> Items, int TotalCount)> GetPagedAsync(FileQueryParameters parameters);

        /// <summary>
        /// Uzantıya göre arar. Gelen değerin NORMALLEŞTİRİLMİŞ olması beklenir
        /// (küçük harf, baştaki nokta eklenmiş) — normalleştirme bir kullanıcı
        /// girdisi kuralı, veritabanı işi değil, o yüzden serviste yapılıyor.
        /// </summary>
        Task<List<TrackedFile>> GetByExtensionAsync(string normalizedExtension);

        /// <summary>Birden fazla kayıtta geçen hash değerleri (boş hash'ler hariç).</summary>
        Task<List<string>> GetDuplicateHashesAsync();

        /// <summary>Verilen hash listesine giren tüm kayıtlar.</summary>
        Task<List<TrackedFile>> GetByHashesAsync(IReadOnlyCollection<string> hashes);

        /// <summary>
        /// Tüm kayıtları dosya yoluna göre sözlük olarak verir. Tarayıcı, her
        /// dosya için ayrı sorgu atmamak (N+1) için bunu tek seferde çekiyor.
        ///
        /// Dönen kayıtlar DEĞİŞİKLİK TAKİBİ AÇIK gelir: tarayıcı bunların
        /// alanlarını güncelleyip SaveChanges ile yazıyor. AsNoTracking
        /// kullanılırsa güncellemeler sessizce kaybolur.
        /// </summary>
        Task<Dictionary<string, TrackedFile>> GetAllByPathAsync();
    }
}
