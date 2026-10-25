using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Admin.Destinations
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly Roma.Services.IDestinationImageService _imageService;

        public CreateModel(ApplicationDbContext db, Roma.Services.IDestinationImageService imageService)
        {
            _db = db;
            _imageService = imageService;
        }

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

            // Process uploaded files (optional) via shared service
            var files = Request.Form?.Files;
            if (files != null && files.Count > 0)
            {
                await _imageService.SaveAsync(Destination.Id, files);
            }

            return RedirectToPage("Index");
        }
    }
}
