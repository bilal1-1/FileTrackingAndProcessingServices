using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileTrackingAndProcessingServices.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// <see cref="IRepository{T}"/>'nin tek ve ortak uygulaması. Yeni bir entity
    /// eklendiğinde bu sınıf tekrar yazılmaz, sadece türetilir.
    /// </summary>
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext Context;

        /// <summary>
        /// Bu tipe karşılık gelen tablo. <c>Set&lt;T&gt;()</c>, DbSet'i çalışma
        /// zamanında tipe göre bulur; generic repository'yi mümkün kılan şey bu.
        ///
        /// protected: türeyen repository'ler kendi sorgularını kurarken kullanır,
        /// ama dışarıya — servislere — açılmaz.
        /// </summary>
        protected DbSet<T> Table => Context.Set<T>();

        public Repository(AppDbContext context)
        {
            Context = context;
        }

        /// <summary>
        /// FindAsync birincil anahtarı EF'in kendi meta verisinden bilir; hangi
        /// alanın anahtar olduğunu tahmin etmeye gerek yok. Ayrıca kayıt zaten
        /// bellekte takip ediliyorsa veritabanına hiç gitmez.
        /// </summary>
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await Table.FindAsync(id);
        }

        public virtual async Task<List<T>> GetAllAsync()
        {
            return await Table.ToListAsync();
        }

        public virtual async Task AddAsync(T entity)
        {
            await Table.AddAsync(entity);
        }

        public virtual void Remove(T entity)
        {
            Table.Remove(entity);
        }
    }
}
