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

        /// <summary>Kaydı silinecekler listesine alır. Yazma işi UnitOfWork'te.</summary>
        void Remove(T entity);
    }
}
