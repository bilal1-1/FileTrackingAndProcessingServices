namespace FileTrackingAndProcessingServices.Models
{
    public class TrackedFile
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Dosya içeriğinin SHA-256 parmak izi (64 karakter, küçük harf hex).
        /// İçerik değiştiğinde yeniden hesaplanır.
        /// </summary>
        public string Hash { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}