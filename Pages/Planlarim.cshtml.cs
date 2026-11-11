using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;

namespace Roma.Pages
{
    public class PlanlarimModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public PlanlarimModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public bool IsAuthenticated { get; set; }
        public List<PlanItemVm> Items { get; set; } = new();
        public string? InfoMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadItemsAsync();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            IsAuthenticated = User?.Identity?.IsAuthenticated == true;
            if (!IsAuthenticated)
            {
                return RedirectToPage();
            }

            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToPage();
            }

            var item = await _db.Itineraries!
                .FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);

            if (item != null)
            {
                var linkedDestinations = await _db.Destinations!
                    .Where(d => EF.Property<int?>(d, "ItineraryId") == item.Id)
                    .ToListAsync();

                foreach (var destination in linkedDestinations)
                {
                    _db.Entry(destination).Property("ItineraryId").CurrentValue = null;
                }

                _db.Itineraries!.Remove(item);
                await _db.SaveChangesAsync();
                InfoMessage = "Plan silindi.";
            }

            await LoadItemsAsync();
            return Page();
        }

        private async Task LoadItemsAsync()
        {
            IsAuthenticated = User?.Identity?.IsAuthenticated == true;
            if (!IsAuthenticated)
            {
                Items = new();
                return;
            }

            var userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                Items = new();
                return;
            }

            var itineraries = await _db.Itineraries!
                .AsNoTracking()
                .Include(i => i.Destinations)
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.StartDate)
                .ToListAsync();

            Items = itineraries.Select(i => new PlanItemVm
            {
                Id = i.Id,
                Title = i.Title,
                StartDate = i.StartDate,
                EndDate = i.EndDate,
                DestinationsText = i.Destinations.Any() ? string.Join(", ", i.Destinations.Select(d => d.Name)) : "-"
            }).ToList();
        }

        public class PlanItemVm
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string DestinationsText { get; set; } = "-";
        }
    }
}