using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roma.Data;
using Roma.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Roma.Services
{
    public interface IDestinationImageService
    {
        Task<DestinationImageSaveResult> SaveAsync(int destinationId, IFormFileCollection? files);
    }

    public class DestinationImageSaveResult
    {
        public List<DestinationImage> Saved { get; } = new List<DestinationImage>();
        public List<(string FileName, string Error)> Errors { get; } = new List<(string, string)>();
        public int Skipped { get; set; }
    }

    public class DestinationImageService : IDestinationImageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DestinationImageService> _logger;

        private const long MaxFileSizeBytes = 5L * 1024 * 1024; // 5 MB
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        public DestinationImageService(IWebHostEnvironment env, ApplicationDbContext db, ILogger<DestinationImageService> logger)
        {
            _env = env;
            _db = db;
            _logger = logger;
        }

        public async Task<DestinationImageSaveResult> SaveAsync(int destinationId, IFormFileCollection? files)
        {
            var result = new DestinationImageSaveResult();
            if (files == null || files.Count == 0) return result;

            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;
            var uploadDir = Path.Combine(webRoot, "uploads", "destinations", destinationId.ToString());
            Directory.CreateDirectory(uploadDir);

            var order = await _db.DestinationImages!
                .Where(i => i.DestinationId == destinationId)
                .CountAsync();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    result.Skipped++;
                    result.Errors.Add((file?.FileName ?? string.Empty, "Empty file."));
                    continue;
                }

                if (file.Length > MaxFileSizeBytes)
                {
                    result.Skipped++;
                    result.Errors.Add((file.FileName, $"File too large. Max {MaxFileSizeBytes} bytes."));
                    continue;
                }

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                {
                    result.Skipped++;
                    result.Errors.Add((file.FileName, "File extension not allowed."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Skipped++;
                    result.Errors.Add((file.FileName, "Invalid content type."));
                    continue;
                }

                try
                {
                    var fileName = System.Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var s = System.IO.File.Create(filePath))
                    {
                        await file.CopyToAsync(s);
                    }

                    var relativeUrl = $"/uploads/destinations/{destinationId}/{fileName}";
                    var image = new DestinationImage
                    {
                        Url = relativeUrl,
                        DestinationId = destinationId,
                        Destination = null!,
                        Order = order++
                    };
                    _db.DestinationImages!.Add(image);
                    result.Saved.Add(image);
                }
                catch (System.Exception ex)
                {
                    result.Skipped++;
                    _logger.LogError(ex, "Destination image upload failed. DestinationId: {DestinationId}, File: {FileName}", destinationId, file.FileName);
                    result.Errors.Add((file.FileName, "Unexpected upload error."));
                }
            }

            if (result.Saved.Count > 0)
            {
                await _db.SaveChangesAsync();
            }

            return result;
        }
    }
}
