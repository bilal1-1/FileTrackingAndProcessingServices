namespace FileTrackingAndProcessingServices.Application.Interfaces
{
    /// <summary>
    /// Her entity için birebir aynı olan temel veritabanı işlemleri. Bir kez
    /// yazılır, tüm tablolarda kullanılır — kod tekrarını önleyen kısım burası.
    ///
    /// Arayüzde bilerek IQueryable dönen bir metot YOK. Olsaydı servisler sorguyu
    /// kendileri kurmaya devam eder, "veritabanı erişimi sadece repository'de"
    /// kuralı kâğıt üzerinde kalırdı. Sorgu kurma işi repository'nin içinde,
    /// korumalı <c>Table</c> özelliği üzerinden yapılır.
    /// </summary>
    public interface IRepository<T> where T : class
    {
        /// <summary>Birincil anahtara göre tek kayıt getirir; yoksa null döner.</summary>
        Task<T?> GetByIdAsync(int id);

        /// <summary>Tablodaki tüm kayıtlar. Küçük tablolar için.</summary>
        Task<List<T>> GetAllAsync();

        /// <summary>
        /// Yeni kaydı ekleme listesine alır. Veritabanına yazmaz —
        /// bunun için <see cref="IUnitOfWork.SaveChangesAsync"/> çağrılmalı.
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>
        /// Mevcut bir kaydın değiştirildiğini bildirir. Yazma işi UnitOfWork'te.
        ///
        /// EF Core, repository'den dönen kaydın alanlarını değiştirdiğinde bunu
        /// kendiliğinden fark eder; bu metot olmadan da güncelleme yazılırdı.
        /// Yine de açıkça duruyor, iki sebeple:
        ///
        /// 1. Niyet kodda görünsün. Ekleme AddAsync, silme Remove ile yapılırken
        ///    güncellemenin karşılığının hiç olmaması, kodu okuyanın güncellemenin
        ///    nerede gerçekleştiğini görememesi demekti.
        /// 2. Sessiz bozulmayı engellesin. Değişiklik takibi kapalı bir sorgudan
        ///    (AsNoTracking) dönen kayıtta alan değiştirmek hiçbir şey yapmaz —
        ///    hata da vermez, güncelleme sessizce kaybolur. Bu metot çağrıldığında
        ///    kayıt takibe alınır ve güncelleme her durumda yazılır.
        /// </summary>
        void Update(T entity);

        /// <summary>Kaydı silinecekler listesine alır. Yazma işi UnitOfWork'te.</summary>
        void Remove(T entity);
    }
}
