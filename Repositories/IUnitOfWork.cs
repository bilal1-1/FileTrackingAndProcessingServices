namespace FileTrackingAndProcessingServices.Repositories
{
    /// <summary>
    /// Biriken değişiklikleri tek seferde veritabanına yazar.
    ///
    /// Neden repository metotlarının içinde SaveChanges çağrılmıyor: her ekleme
    /// kendi başına kaydedilirse 10 kayıt için 10 ayrı veritabanı turu ve 10 ayrı
    /// transaction oluşur; ortada bir hata olursa yarısı yazılmış, yarısı
    /// yazılmamış bir durum kalır. Kaydetme anını çağıran taraf belirlemeli.
    ///
    /// EF Core'un DbContext'i zaten bu işi yapıyor; buradaki arayüzün amacı onu
    /// servislerden gizlemek, böylece servisler DbContext'i hiç tanımıyor.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>Etkilenen satır sayısını döner.</summary>
        Task<int> SaveChangesAsync();
    }
}
