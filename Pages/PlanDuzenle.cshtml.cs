using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;

namespace Roma.Pages
{
    public class PlanDuzenleModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public PlanDuzenleModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public int Id { get; set; }

        [BindProperty]
        public string Title { get; set; } = string.Empty;

        [BindProperty]
        public DateTime StartDate { get; set; }

        [BindProperty]
        public DateTime EndDate { get; set; }

        [BindProperty]
        public int? DestinationId { get; set; }

        public string? ErrorMessage { get; set; }
        public List<DestinationOptionVm> Destinations { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Planlarim");
            }

            await LoadDestinationsAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var itinerary = await _db.Itineraries!
                .Include(i => i.Destinations)
                .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (itinerary == null)
            {
                ErrorMessage = "Plan bulunamadı.";
                return Page();
            }

            Id = itinerary.Id;
            Title = itinerary.Title;
            StartDate = itinerary.StartDate;
            EndDate = itinerary.EndDate;
            DestinationId = itinerary.Destinations.FirstOrDefault()?.Id;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Planlarim");
            }

            await LoadDestinationsAsync();

            if (string.IsNullOrWhiteSpace(Title))
            {
                ModelState.AddModelError(nameof(Title), "Başlık zorunludur.");
                return Page();
            }

            if (!DestinationId.HasValue || DestinationId <= 0)
            {
                ModelState.AddModelError(nameof(DestinationId), "Destinasyon seçiniz.");
                return Page();
            }

            if (EndDate < StartDate)
            {
                ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıçtan önce olamaz.");
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var itinerary = await _db.Itineraries!
                .Include(i => i.Destinations)
                .FirstOrDefaultAsync(i => i.Id == Id && i.UserId == userId);

            if (itinerary == null)
            {
                ErrorMessage = "Plan bulunamadı.";
                return Page();
            }

            var destination = await _db.Destinations!.FirstOrDefaultAsync(d => d.Id == DestinationId.Value);
            if (destination == null)
            {
                ModelState.AddModelError(nameof(DestinationId), "Geçerli bir destinasyon seçiniz.");
                return Page();
            }

            itinerary.Title = Title.Trim();
            itinerary.StartDate = StartDate.Date;
            itinerary.EndDate = EndDate.Date;
            itinerary.Destinations.Clear();
            itinerary.Destinations.Add(destination);

            await _db.SaveChangesAsync();
            return RedirectToPage("/Planlarim");
        }

        private async Task LoadDestinationsAsync()
        {
            Destinations = await _db.Destinations!
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DestinationOptionVm { Id = d.Id, Name = d.Name })
                .ToListAsync();
        }

        public class DestinationOptionVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}