using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Destinations
{
    [AllowAnonymous]
    public class PhotoDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public PhotoDetailsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public Destination? Destination { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Destination = await _db.Destinations!
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .Include(d => d.Hotels)
                .Include(d => d.Meals)
                .Include(d => d.Flavors)
                .Include(d => d.Parkings)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (Destination == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
