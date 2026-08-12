using FileTrackingAndProcessingServices.Models;
using FileTrackingAndProcessingServices.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileTrackingAndProcessingServices.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileTrackingService _fileService;
        private readonly IFolderScannerService _scannerService;

        public FilesController(
            IFileTrackingService fileService,
            IFolderScannerService scannerService)
        {
            _fileService = fileService;
            _scannerService = scannerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var files = await _fileService.GetAllFilesAsync();
            return Ok(files);
        }

        [HttpPost("scan")]
        public async Task<IActionResult> Scan()
        {
            var newFileCount = await _scannerService.ScanFolderAsync();
            return Ok(new { message = $"{newFileCount} yeni dosya işlendi." });
        }
    }
}