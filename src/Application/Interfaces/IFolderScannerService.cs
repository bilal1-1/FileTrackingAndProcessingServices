namespace FileTrackingAndProcessingServices.Services
{
    public interface IFolderScannerService
    {
        Task<int> ScanFolderAsync();
    }
}