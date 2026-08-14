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
        public async Task<IActionResult> GetAll([FromQuery] FileQueryParameters parameters)
        {
            var result = await _fileService.GetAllFilesAsync(parameters);
            return Ok(result);
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

        /// <summary>
        /// İçeriği birebir aynı olan dosyaları hash'e göre gruplayıp döner.
        /// Rota, "{id}" kalıbından önce eşleşir: ASP.NET Core yönlendirmesinde
        /// sabit metinli segmentler parametreli olanlara göre önceliklidir.
        /// </summary>
        [HttpGet("duplicates")]
        public async Task<IActionResult> GetDuplicates()
        {
            var groups = await _fileService.GetDuplicatesAsync();
            return Ok(groups);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string extension)
        {
            var files = await _fileService.SearchByExtensionAsync(extension);
            return Ok(files);
        }
    }
}
