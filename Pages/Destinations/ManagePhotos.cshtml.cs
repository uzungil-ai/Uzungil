using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using System;
using System.Threading.Channels;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Roma.Pages.Destinations
{
    public class ManagePhotosModel : PageModel
    {
        private const long MaxFileSize = 5L * 1024 * 1024; // 5 MB
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly Channel<Roma.Services.AiJob>? _aiChannel;
        private readonly Roma.Services.ImageProcessingService? _imgSvc;
        private readonly ILogger<ManagePhotosModel> _logger;

        public ManagePhotosModel(ApplicationDbContext db, IWebHostEnvironment env, ILogger<ManagePhotosModel> logger, Channel<Roma.Services.AiJob>? aiChannel = null, Roma.Services.ImageProcessingService? imgSvc = null)
        {
            _db = db;
            _env = env;
            _aiChannel = aiChannel;
            _imgSvc = imgSvc;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostReorderAsync(int id)
        {
            string body;
            using (var sr = new StreamReader(Request.Body))
            {
                body = await sr.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(body)) return BadRequest();

            List<int>? ids;
            try
            {
                ids = System.Text.Json.JsonSerializer.Deserialize<List<int>>(body);
            }
            catch
            {
                return BadRequest();
            }

            if (ids == null || ids.Count == 0) return BadRequest();

            var images = await _db.DestinationImages!.Where(di => di.DestinationId == id && ids.Contains(di.Id)).ToListAsync();
            for (int i = 0; i < ids.Count; i++)
            {
                var img = images.FirstOrDefault(x => x.Id == ids[i]);
                if (img != null) img.Order = i;
            }

            _db.DestinationImages.UpdateRange(images);
            await _db.SaveChangesAsync();

            return new JsonResult(new { success = true });
        }

        [BindProperty]
        public IFormFile[]? Uploads { get; set; }

        public Destination? Destination { get; set; }
        public List<DestinationImage>? Images { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                _logger.LogWarning("ManagePhotos requested without id");
                return BadRequest("destination id is required");
            }

            try
            {
                Destination = await _db.Destinations!.Include(d => d.Images).FirstOrDefaultAsync(d => d.Id == id.Value);
                if (Destination == null)
                {
                    _logger.LogInformation("ManagePhotos: destination with id {Id} not found", id.Value);
                    return NotFound($"Destination {id.Value} not found");
                }
                Images = Destination.Images;
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ManagePhotos: error loading destination {Id}", id.Value);
                throw; // let exception propagate so developer exception page shows details in dev
            }
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            Destination = await _db.Destinations!.Include(d => d.Images).FirstOrDefaultAsync(d => d.Id == id);
            if (Destination == null) return NotFound();

            if (Uploads != null && Uploads.Length > 0)
            {
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "destinations", id.ToString());
                Directory.CreateDirectory(uploadDir);

                foreach (var file in Uploads)
                {
                    if (file.Length > 0)
                    {
                        var ext = Path.GetExtension(file.FileName);
                        var fileName = Guid.NewGuid().ToString("N") + ext;
                        var filePath = Path.Combine(uploadDir, fileName);
                        using (var stream = System.IO.File.Create(filePath))
                        {
                            await file.CopyToAsync(stream);
                        }

                        var relativeUrl = $"/uploads/destinations/{id}/{fileName}";
                        var image = new DestinationImage { Url = relativeUrl, DestinationId = id, Destination = Destination, Order = Destination.Images.Count };
                        _db.DestinationImages!.Add(image);
                        Destination.Images.Add(image);
                    }
                }

                await _db.SaveChangesAsync();
            }

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostUploadAsync(int id)
        {
            Destination = await _db.Destinations!.Include(d => d.Images).FirstOrDefaultAsync(d => d.Id == id);
            if (Destination == null) return NotFound();
            var files = Request.Form.Files;
            if (files == null || files.Count == 0) return new JsonResult(new { success = false, uploaded = 0, message = "No files received by server." });

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "destinations", id.ToString());
            Directory.CreateDirectory(uploadDir);

            int addedCount = 0;
            int skippedCount = 0;
            var errors = new List<string>();

            try
            {
                foreach (var file in files)
                {
                    if (file.Length <= 0) { skippedCount++; errors.Add($"{file.FileName}: empty"); continue; }
                    if (file.Length > MaxFileSize) { skippedCount++; errors.Add($"{file.FileName}: too large"); continue; }
                    if (string.IsNullOrEmpty(file.ContentType) || !file.ContentType.StartsWith("image/")) { skippedCount++; errors.Add($"{file.FileName}: invalid content type ({file.ContentType})"); continue; }

                    var ext = Path.GetExtension(file.FileName);
                    var fileName = Guid.NewGuid().ToString("N") + ext;
                    var filePath = Path.Combine(uploadDir, fileName);
                    using (var stream = System.IO.File.Create(filePath))
                    {
                        await file.CopyToAsync(stream);
                    }

                    string relativeUrl = $"/uploads/destinations/{id}/{fileName}";
                    if (_imgSvc != null)
                    {
                        using (var fs = System.IO.File.OpenRead(filePath))
                        {
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            var outPath = await _imgSvc.SaveResizedAsync(fs, uploadDir, fileNameWithoutExt);
                            var rel = outPath.Replace(_env.WebRootPath, "").Replace('\\', '/').TrimStart('/');
                            relativeUrl = "/" + rel;
                        }
                    }

                    var image = new DestinationImage { Url = relativeUrl, DestinationId = id, Destination = Destination, Order = Destination.Images.Count };
                    _db.DestinationImages!.Add(image);
                    Destination.Images.Add(image);
                    await _db.SaveChangesAsync();

                    try
                    {
                        if (_aiChannel != null)
                        {
                            var job = new Roma.Services.AiJob { DestinationId = id, ImageId = image.Id, JobType = "AltText" };
                            _aiChannel.Writer.TryWrite(job);
                        }
                    }
                    catch { /* non-fatal */ }

                    addedCount++;
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, uploaded = addedCount, skipped = skippedCount, message = ex.Message, errors });
            }

            var resultImages = Destination.Images.Select(i => new { id = i.Id, url = i.Url }).ToList();
            return new JsonResult(new { success = true, uploaded = addedCount, skipped = skippedCount, images = resultImages, errors });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, int imageId)
        {
            var img = await _db.DestinationImages!.FirstOrDefaultAsync(di => di.Id == imageId && di.DestinationId == id);
            if (img == null) return NotFound();

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var rel = img.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.Combine(webRoot, rel);
                if (System.IO.File.Exists(full)) System.IO.File.Delete(full);
            }
            catch { /* ignore file delete errors */ }

            _db.DestinationImages!.Remove(img);
            await _db.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostMoveAsync(int id, int imageId, bool left)
        {
            var images = await _db.DestinationImages!.Where(di => di.DestinationId == id).OrderBy(di => di.Order).ToListAsync();
            var idx = images.FindIndex(x => x.Id == imageId);
            if (idx < 0) return NotFound();
            int swapWith = left ? idx - 1 : idx + 1;
            if (swapWith < 0 || swapWith >= images.Count) return RedirectToPage(new { id });

            var current = images[idx];
            var other = images[swapWith];
            var tmp = current.Order;
            current.Order = other.Order;
            other.Order = tmp;

            _db.DestinationImages!.UpdateRange(current, other);
            await _db.SaveChangesAsync();
            return RedirectToPage(new { id });
        }
    }
}
