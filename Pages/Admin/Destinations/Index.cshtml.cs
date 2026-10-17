using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db) => _db = db;

        public List<Destination> Items { get; set; } = new List<Destination>();

        public async Task OnGetAsync()
        {
            Items = await _db.Destinations!.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
        }
    }
}
