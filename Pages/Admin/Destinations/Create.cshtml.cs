using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public CreateModel(ApplicationDbContext db) => _db = db;

        [BindProperty]
        public Destination Destination { get; set; } = new Destination { Name = string.Empty, Country = string.Empty, Description = string.Empty };

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            _db.Destinations!.Add(Destination);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
