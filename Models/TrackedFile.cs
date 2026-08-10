namespace FileTrackingAndProcessingServices.Models
{
    public class TrackedFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Extension { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
    }
}