namespace FileTrackingAndProcessingServices.Models
{
    /// <summary>
    /// GET /api/files için sorgu parametreleri.
    /// Sınırlar property'lerin içinde korunur, böylece istemci ne gönderirse
    /// göndersin servis katmanına geçersiz değer ulaşmaz.
    /// </summary>
    public class FileQueryParameters
    {
        // Tek istekte dönebilecek en fazla kayıt. İstemci 10.000 isteyip
        // sunucuyu zorlayamasın diye üst sınır konuyor.
        private const int MaxPageSize = 100;

        private int _page = 1;
        private int _pageSize = 10;

        public int Page
        {
            get => _page;
            set => _page = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1 ? 1 : (value > MaxPageSize ? MaxPageSize : value);
        }

        /// <summary>
        /// Sıralanacak alan: fileName, extension, sizeBytes, createdAt, modifiedAt.
        /// Tanınmayan bir değer gelirse Id'ye göre sıralanır.
        /// </summary>
        public string SortBy { get; set; } = "id";

        /// <summary>
        /// asc (varsayılan) veya desc.
        /// </summary>
        public string SortOrder { get; set; } = "asc";
    }
}
