using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Destinations
{
    public class DownloadModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public DownloadModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<IActionResult> OnGetAsync(int destinationId, int imageId)
        {
            var img = await _db.DestinationImages!.FirstOrDefaultAsync(i => i.Id == imageId && i.DestinationId == destinationId);
            if (img == null) return NotFound();

            // identify user / session
            var userId = User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
            var sessionId = HttpContext.Session.Id;

            // count previous downloads for this session/user on this destination
            var previousCount = await _db.DownloadRecords!.Where(d => d.DestinationId == destinationId && (d.SessionId == sessionId || d.UserId == userId)).CountAsync();

            bool isFree = previousCount == 0; // first download free per user/session
            decimal amount = isFree ? 0m : 1.00m; // example price

            if (!isFree)
            {
                // redirect to payment provider or show payment page
                // For now, show simple page informing payment required
                TempData["PaymentRequired"] = "Bu görselin indirilmesi ücretlidir. Ödeme ekranına yönlendiriliyorsunuz.";
                // In a real app, integrate payment gateway here and on success redirect back with a token
                return RedirectToPage("/Payment/Checkout", new { destinationId, imageId });
            }

            // Log download record as free
            var rec = new DownloadRecord
            {
                DestinationId = destinationId,
                ImageId = imageId,
                UserId = userId,
                SessionId = sessionId,
                IsPaid = false,
                Amount = 0m
            };
            _db.DownloadRecords!.Add(rec);
            await _db.SaveChangesAsync();

            // serve the file
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rel = img.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.Combine(webRoot, rel);
            if (!System.IO.File.Exists(full)) return NotFound();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(full);
            var contentType = "application/octet-stream";
            if (full.EndsWith('.')) contentType = "application/octet-stream";
            return File(fileBytes, contentType, Path.GetFileName(full));
        }
    }
}
