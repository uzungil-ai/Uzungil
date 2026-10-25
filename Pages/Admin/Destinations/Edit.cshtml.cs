using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly Roma.Services.IDestinationImageService _imageService;

        public EditModel(ApplicationDbContext db, Roma.Services.IDestinationImageService imageService)
        {
            _db = db;
            _imageService = imageService;
        }

        [BindProperty]
        public Destination Destination { get; set; } = new Destination { Name = string.Empty, Country = string.Empty, Description = string.Empty };

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Destination = await _db.Destinations!.FirstOrDefaultAsync(d => d.Id == id) ?? new Destination { Name = string.Empty, Country = string.Empty, Description = string.Empty };
            if (Destination.Id == 0) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var existing = await _db.Destinations!.FirstOrDefaultAsync(d => d.Id == Destination.Id);
            if (existing == null) return NotFound();

            existing.Name = Destination.Name;
            existing.Country = Destination.Country;
            existing.Description = Destination.Description;
            existing.EntranceFee = Destination.EntranceFee;
            existing.Latitude = Destination.Latitude;
            existing.Longitude = Destination.Longitude;

            await _db.SaveChangesAsync();

            // Handle uploaded files (optional) via shared service
            var files = Request.Form?.Files;
            if (files != null && files.Count > 0)
            {
                await _imageService.SaveAsync(Destination.Id, files);
            }

            return RedirectToPage("Index");
        }
    }
}
