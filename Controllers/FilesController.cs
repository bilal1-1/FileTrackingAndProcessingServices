using Microsoft.AspNetCore.Mvc;
using FileTrackingAndProcessingServices.Services;

namespace FileTrackingAndProcessingServices.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        private readonly IFileTrackingService _service;

        public FilesController(IFileTrackingService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var files = await _service.GetAllFilesAsync();
            return Ok(files);
        }
    }
}