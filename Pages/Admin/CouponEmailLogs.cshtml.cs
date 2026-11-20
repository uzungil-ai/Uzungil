using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CouponEmailLogsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CouponEmailLogsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<SentEmail> Items { get; set; } = new();

        public async Task OnGetAsync()
        {
            Items = await _db.SentEmails!
                .AsNoTracking()
                .Where(x => EF.Functions.Like(x.Subject, "%İndirim Kuponu%"))
                .OrderByDescending(x => x.SentAt)
                .Take(500)
                .ToListAsync();
        }
    }
}