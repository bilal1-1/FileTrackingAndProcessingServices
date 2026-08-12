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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var file = await _fileService.GetByIdAsync(id);
            if (file == null)
            {
                return NotFound(new { message = $"{id} numaralı dosya bulunamadı." });
            }
            return Ok(file);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string extension)
        {
            var files = await _fileService.SearchByExtensionAsync(extension);
            return Ok(files);
        }
    }
}