using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using System.Threading.Channels;

namespace Roma.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class UploadsController : Controller
    {
        private const long MaxFileSize = 5L * 1024 * 1024; // 5 MB
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly Channel<Roma.Services.AiJob>? _aiChannel;
        private readonly Roma.Services.ImageProcessingService? _imgSvc;

        public UploadsController(ApplicationDbContext db, IWebHostEnvironment env, Channel<Roma.Services.AiJob>? aiChannel = null, Roma.Services.ImageProcessingService? imgSvc = null)
        {
            _db = db;
            _env = env;
            _aiChannel = aiChannel;
            _imgSvc = imgSvc;
        }

        // GET: Admin/Uploads/Index?id={destinationId}
        public async Task<IActionResult> Index(int id)
        {
            var destination = await _db.Destinations!.Include(d => d.Images).FirstOrDefaultAsync(d => d.Id == id);
            if (destination == null) return NotFound();
            return View(destination);
        }

        // POST: Admin/Uploads/Upload?id={destinationId}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int id)
        {
            var destination = await _db.Destinations!.Include(d => d.Images).FirstOrDefaultAsync(d => d.Id == id);
            if (destination == null) return NotFound();

            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
            {
                TempData["UploadMessage"] = "No files selected.";
                return RedirectToAction(nameof(Index), new { id });
            }

            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "destinations", id.ToString());
            Directory.CreateDirectory(uploadDir);

            int added = 0;
            var addedImages = new System.Collections.Generic.List<DestinationImage>();
            foreach (var file in files)
            {
                if (file.Length <= 0) continue;
                if (file.Length > MaxFileSize) continue;
                if (string.IsNullOrEmpty(file.ContentType) || !file.ContentType.StartsWith("image/")) continue;

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
                    try
                    {
                        using (var fs = System.IO.File.OpenRead(filePath))
                        {
                            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            var outPath = await _imgSvc.SaveResizedAsync(fs, uploadDir, fileNameWithoutExt);
                            var rel = outPath.Replace(_env.WebRootPath, "").Replace('\\', '/').TrimStart('/');
                            relativeUrl = "/" + rel;
                        }
                    }
                    catch
                    {
                        // ignore image service failures, use original file
                    }
                }

                var image = new DestinationImage { Url = relativeUrl, DestinationId = id, Destination = destination, Order = destination.Images.Count };
                _db.DestinationImages!.Add(image);
                destination.Images.Add(image);
                await _db.SaveChangesAsync();

                // enqueue alt-text job if channel available
                try
                {
                    if (_aiChannel != null)
                    {
                        var job = new Roma.Services.AiJob { DestinationId = id, ImageId = image.Id, JobType = "AltText" };
                        await _aiChannel.Writer.WriteAsync(job);
                    }
                }
                catch
                {
                    // non-fatal
                }

                added++;
                addedImages.Add(image);
            }

            // If AJAX request, return JSON with newly added image info so client can update DOM
            var isAjax = Request.Headers != null && 
                         (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"));

            if (isAjax)
            {
                var result = addedImages.Select(ai => new { id = ai.Id, url = ai.Url, alt = ai.AltText ?? string.Empty, order = ai.Order }).ToList();
                return new JsonResult(new { success = true, added = result });
            }

            TempData["UploadMessage"] = $"Uploaded {added} files.";
            return RedirectToAction(nameof(Index), new { id });
        }
    }
}
