using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    [Authorize(Roles = "Admin")]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public DeleteModel(ApplicationDbContext db) => _db = db;

        [BindProperty]
        public Destination Destination { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var dest = await _db.Destinations!.Include(d => d.Images).FirstOrDefaultAsync(d => d.Id == id);
            if (dest == null) return NotFound();
            Destination = dest;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var existing = await _db.Destinations!.FirstOrDefaultAsync(d => d.Id == Destination.Id);
            if (existing == null) return NotFound();
            _db.Destinations!.Remove(existing);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
