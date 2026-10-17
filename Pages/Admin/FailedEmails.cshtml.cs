using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class FailedEmailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public FailedEmailsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<FailedEmail> Items { get; set; } = new List<FailedEmail>();

        public async Task OnGetAsync()
        {
            Items = await _db.FailedEmails!.OrderByDescending(f => f.CreatedAt).Take(200).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync([FromForm] int[] ids)
        {
            if (ids != null && ids.Length > 0)
            {
                var toDelete = await _db.FailedEmails!.Where(f => ids.Contains(f.Id)).ToListAsync();
                _db.FailedEmails!.RemoveRange(toDelete);
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
