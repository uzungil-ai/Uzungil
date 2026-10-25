using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    [ApiController]
    [Route("api/TestUpload")]
    public class UploadController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UploadController> _logger;
        private readonly Roma.Services.IDestinationImageService _imageService;

        private const int MaxRequestsPerMinute = 30;

        public UploadController(
            ApplicationDbContext db,
            IMemoryCache cache,
            ILogger<UploadController> logger,
            Roma.Services.IDestinationImageService imageService)
        {
            _db = db;
            _cache = cache;
            _logger = logger;
            _imageService = imageService;
        }

        // POST api/upload/{id}
        [HttpPost("{id}")]
        public async Task<IActionResult> Post(int id)
        {
            var rateLimitKey = GetRateLimitKey();
            var currentCount = _cache.Get<int?>(rateLimitKey) ?? 0;
            if (currentCount >= MaxRequestsPerMinute)
            {
                Response.Headers["Retry-After"] = "60";
                return StatusCode(429, new
                {
                    success = false,
                    message = "Rate limit exceeded. Try again in a minute."
                });
            }
            _cache.Set(rateLimitKey, currentCount + 1, TimeSpan.FromMinutes(1));

            var destinationExists = await _db.Destinations!.AnyAsync(d => d.Id == id);
            if (!destinationExists)
            {
                return NotFound(new { success = false, message = "Destination not found." });
            }

            if (!Request.HasFormContentType)
            {
                return BadRequest(new { success = false, message = "Expected multipart/form-data." });
            }

            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { success = false, message = "No files provided." });
            }

            var saveResult = await _imageService.SaveAsync(id, files);

            var images = saveResult.Saved.Select(i => new { id = i.Id, url = i.Url, storedName = Path.GetFileName(i.Url) }).ToList();
            var errors = saveResult.Errors.Select(e => new { file = e.FileName, error = e.Error }).ToList();

            return Ok(new
            {
                success = true,
                destinationId = id,
                total = files.Count,
                uploaded = saveResult.Saved.Count,
                skipped = saveResult.Skipped,
                images,
                errors
            });
        }

        private string GetRateLimitKey()
        {
            var userId = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return $"upload-rate:{userId}";
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return $"upload-rate:{ip}";
        }
    }
}
