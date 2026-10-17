using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Payment
{
    public class CheckoutModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public CheckoutModel(ApplicationDbContext db) { _db = db; }

        public IActionResult OnGet(int destinationId, int imageId)
        {
            ViewData["DestinationId"] = destinationId;
            ViewData["ImageId"] = imageId;
            return Page();
        }

        public async Task<IActionResult> OnPostCompleteAsync(int destinationId, int imageId)
        {
            // For demo: mark a DownloadRecord as paid and create if not exists
            var sessionId = HttpContext.Session.Id;
            var userId = User?.Identity?.IsAuthenticated == true ? User.Identity.Name : null;

            var rec = _db.DownloadRecords!.FirstOrDefault(d => d.DestinationId == destinationId && d.ImageId == imageId && (d.SessionId == sessionId || d.UserId == userId));
            if (rec == null)
            {
                rec = new DownloadRecord { DestinationId = destinationId, ImageId = imageId, SessionId = sessionId, UserId = userId, IsPaid = true, Amount = 1.00m };
                _db.DownloadRecords.Add(rec);
            }
            else
            {
                rec.IsPaid = true; rec.Amount = 1.00m;
                _db.DownloadRecords.Update(rec);
            }

            await _db.SaveChangesAsync();

            // redirect to actual file serving
            return RedirectToPage("/Destinations/Download", new { destinationId, imageId });
        }
    }
}
