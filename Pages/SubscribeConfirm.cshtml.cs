using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Models;
using Microsoft.EntityFrameworkCore;

namespace Roma.Pages
{
    public class SubscribeConfirmModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SubscribeConfirmModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public bool Success { get; set; }

        public async Task OnGetAsync(string email, string token)
        {
            Success = false;
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token)) return;

            var sub = await _db.Subscribers!.FirstOrDefaultAsync(s => s.Email == email && s.ConfirmationToken == token);
            if (sub != null && !sub.IsConfirmed)
            {
                if (sub.ConfirmationExpiresAt != null && sub.ConfirmationExpiresAt < System.DateTime.UtcNow)
                {
                    // token expired
                    Success = false;
                    return;
                }

                sub.IsConfirmed = true;
                sub.ConfirmationToken = null;
                sub.ConfirmationExpiresAt = null;
                // ensure unsubscribe token exists
                if (string.IsNullOrEmpty(sub.UnsubscribeToken))
                {
                    sub.UnsubscribeToken = System.Guid.NewGuid().ToString("N");
                }
                await _db.SaveChangesAsync();
                Success = true;
            }
        }
    }
}
