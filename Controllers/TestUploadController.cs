using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UploadController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UploadController> _logger;

        private const long MaxFileSizeBytes = 5L * 1024 * 1024;
        private const int MaxRequestsPerMinute = 30;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        public UploadController(
            ApplicationDbContext db,
            IWebHostEnvironment env,
            IMemoryCache cache,
            ILogger<UploadController> logger)
        {
            _db = db;
            _env = env;
            _cache = cache;
            _logger = logger;
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

            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
            var uploadDir = Path.Combine(webRoot, "uploads", "destinations", id.ToString());
            Directory.CreateDirectory(uploadDir);

            var added = 0;
            var skipped = 0;
            var images = new List<object>();
            var errors = new List<object>();
            var order = await _db.DestinationImages!
                .Where(i => i.DestinationId == id)
                .CountAsync();

            foreach (var file in files)
            {
                if (file.Length == 0)
                {
                    skipped++;
                    errors.Add(new { file = file.FileName, error = "Empty file." });
                    continue;
                }

                if (file.Length > MaxFileSizeBytes)
                {
                    skipped++;
                    errors.Add(new { file = file.FileName, error = $"File too large. Max {MaxFileSizeBytes} bytes." });
                    continue;
                }

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                {
                    skipped++;
                    errors.Add(new { file = file.FileName, error = "File extension not allowed." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    errors.Add(new { file = file.FileName, error = "Invalid content type." });
                    continue;
                }

                try
                {
                    var fileName = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var s = System.IO.File.Create(filePath))
                    {
                        await file.CopyToAsync(s);
                    }

                    var relativeUrl = $"/uploads/destinations/{id}/{fileName}";
                    var image = new DestinationImage
                    {
                        Url = relativeUrl,
                        DestinationId = id,
                        Destination = null!,
                        Order = order++
                    };
                    _db.DestinationImages!.Add(image);
                    await _db.SaveChangesAsync();

                    images.Add(new { id = image.Id, url = image.Url, originalName = file.FileName, size = file.Length });
                    added++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    _logger.LogError(ex, "Failed to upload file {FileName} for destination {DestinationId}", file.FileName, id);
                    errors.Add(new { file = file.FileName, error = "Unexpected upload error." });
                }
            }

            return Ok(new
            {
                success = true,
                destinationId = id,
                total = files.Count,
                uploaded = added,
                skipped,
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
