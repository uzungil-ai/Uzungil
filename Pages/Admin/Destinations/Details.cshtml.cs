using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    [Authorize(Roles = "Admin")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public DetailsModel(ApplicationDbContext db) => _db = db;

        public Destination? Destination { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Destination = await _db.Destinations!
                .Include(d => d.Images)
                .Include(d => d.Meals)
                .Include(d => d.Hotels)
                .Include(d => d.Parkings)
                .Include(d => d.Recommendations)
                .Include(d => d.Attractions)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (Destination == null) return NotFound();
            return Page();
        }
    }
}
