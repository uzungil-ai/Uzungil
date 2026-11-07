using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;

namespace Roma.Pages
{
    public class PlanDetayModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public PlanDetayModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public PlanItemVm? Item { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                ErrorMessage = "Plan detayını görmek için giriş yapmalısınız.";
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                ErrorMessage = "Kullanıcı bilgisi alınamadı.";
                return Page();
            }

            var itinerary = await _db.Itineraries!
                .AsNoTracking()
                .Include(i => i.Destinations)
                .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (itinerary == null)
            {
                ErrorMessage = "Plan bulunamadı.";
                return Page();
            }

            Item = new PlanItemVm
            {
                Title = itinerary.Title,
                StartDate = itinerary.StartDate,
                EndDate = itinerary.EndDate,
                DestinationsText = itinerary.Destinations.Any() ? string.Join(", ", itinerary.Destinations.Select(d => d.Name)) : "-"
            };

            return Page();
        }

        public class PlanItemVm
        {
            public string Title { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string DestinationsText { get; set; } = "-";
        }
    }
}