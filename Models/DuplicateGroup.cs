namespace FileTrackingAndProcessingServices.Models
{
    /// <summary>
    /// Aynı SHA-256 parmak izine sahip, yani içeriği birebir aynı olan
    /// dosyaların oluşturduğu grup.
    /// </summary>
    public class DuplicateGroup
    {
        /// <summary>Gruptaki tüm dosyaların ortak hash değeri.</summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>Tek bir kopyanın boyutu. İçerik aynı olduğu için hepsi eşit.</summary>
        public long SizeBytes { get; set; }

        /// <summary>Gruptaki dosya sayısı (her zaman 2 veya daha fazla).</summary>
        public int Count { get; set; }

        /// <summary>
        /// Bu grup yüzünden boşa giden alan: tek kopya bırakılsa geri kazanılacak
        /// bayt miktarı. SizeBytes * (Count - 1).
        /// </summary>
        public long WastedBytes { get; set; }

        public List<TrackedFile> Files { get; set; } = new();
    }
}
