namespace FileTrackingAndProcessingServices.Models
{
    public class FolderWatchSettings
    {
        public string FolderPath { get; set; } = string.Empty;
        public int ScanIntervalSeconds { get; set; }
    }
}