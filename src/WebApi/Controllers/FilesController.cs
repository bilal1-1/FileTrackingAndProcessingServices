using FileTrackingAndProcessingServices.Application.Interfaces;
using FileTrackingAndProcessingServices.Application.Models;
using FileTrackingAndProcessingServices.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileTrackingAndProcessingServices.WebApi.Controllers
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
            var result = await _fileService.GetAllFilesAsync(parameters, HttpContext.RequestAborted);
            return Ok(result);
        }

        /// <summary>
        /// Taramayı elle tetikler.
        ///
        /// HttpContext.RequestAborted, istemci bağlantıyı kapattığında iptal
        /// olur; tarama da o noktada durdurulabilsin diye aşağı geçiriliyor.
        /// Aynı anda başka bir tarama koşuyorsa (ör. arka plan servisi) bu istek
        /// onun bitmesini bekler, paralel çalışmaz.
        /// </summary>
        [HttpPost("scan")]
        public async Task<IActionResult> Scan()
        {
            var newFileCount = await _scannerService.ScanFolderAsync(HttpContext.RequestAborted);
            return Ok(new { message = $"{newFileCount} yeni dosya işlendi." });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var file = await _fileService.GetByIdAsync(id, HttpContext.RequestAborted);
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
        public async Task<IActionResult> GetDuplicates([FromQuery] FileQueryParameters parameters)
        {
            var groups = await _fileService.GetDuplicatesAsync(parameters, HttpContext.RequestAborted);
            return Ok(groups);
        }

        /// <summary>
        /// Uzantıya göre arama yapar. Uzantı noktalı da noktasız da yazılabilir:
        /// "pdf" ile ".pdf" aynı sonucu döner, büyük/küçük harf de önemsizdir.
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string? extension,
            [FromQuery] FileQueryParameters parameters)
        {
            // Parametre hiç verilmezse bağlayıcı null geçiriyordu ve arama
            // sırasında NullReferenceException oluşup 500 dönüyordu. Eksik
            // parametre bir sunucu hatası değil, istemci hatasıdır: 400 daha
            // doğru ve mesaj kullanıcıya ne yapması gerektiğini söylüyor.
            if (string.IsNullOrWhiteSpace(extension))
            {
                return BadRequest(new
                {
                    message = "extension parametresi zorunludur. Örnek: /api/files/search?extension=pdf"
                });
            }

            var files = await _fileService.SearchByExtensionAsync(
                extension, parameters, HttpContext.RequestAborted);

            return Ok(files);
        }
    }
}
