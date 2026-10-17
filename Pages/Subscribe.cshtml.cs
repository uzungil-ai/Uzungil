using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages
{
    public class SubscribeModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SubscribeModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string Email { get; set; } = null!;
        [BindProperty]
        public bool Consent { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || !Consent)
            {
                ModelState.AddModelError("", "E-posta ve rıza gereklidir.");
                return Page();
            }

            var exists = await _db.Subscribers!.AnyAsync(s => s.Email == Email);
            if (!exists)
            {
                var token = System.Guid.NewGuid().ToString("N");
                var expires = System.DateTime.UtcNow.AddDays(7);
                var sub = new Subscriber { Email = Email, ConsentGiven = true, IsConfirmed = false, CreatedAt = System.DateTime.UtcNow, ConfirmationToken = token, ConfirmationExpiresAt = expires };
                // store token in a simple way: use Url with token param; persist token in-memory by adding to subscriber via TempData is not reliable
                // For simplicity, add a ConfirmationToken property to Subscriber at runtime if available
                // Here we set IsConfirmed=false and send confirmation email with token link using email queue if available
                _db.Subscribers!.Add(sub);
                await _db.SaveChangesAsync();

                // send confirmation email if queue available
                try
                {
                    var link = $"{Request.Scheme}://{Request.Host}/Subscribe/Confirm?email={System.Uri.EscapeDataString(Email)}&token={token}";
                    var body = $"Lütfen aboneliğinizi onaylamak için şu linke tıklayın (7 gün içinde geçerlidir): {link}";
                    var queue = HttpContext.RequestServices.GetService(typeof(Roma.Services.IEmailQueue)) as Roma.Services.IEmailQueue;
                    if (queue != null)
                    {
                        await queue.EnqueueAsync(new Roma.Services.EmailMessage(Email, "Abonelik Onayı", body));
                    }
                }
                catch
                {
                }
            }

            return RedirectToPage("/Index");
        }
    }
}
