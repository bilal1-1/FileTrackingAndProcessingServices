namespace FileTrackingAndProcessingServices.Application.Interfaces
{
    public interface IFolderScannerService
    {
        Task<int> ScanFolderAsync(CancellationToken cancellationToken = default);
    }
}
