using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using Microsoft.Extensions.Logging;

namespace Roma.Pages
{
    [Authorize]
    public class SeyahatPlanlaModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<SeyahatPlanlaModel> _logger;

        public SeyahatPlanlaModel(ApplicationDbContext db, ILogger<SeyahatPlanlaModel> logger)
        {
            _db = db;
            _logger = logger;
        }

        [BindProperty]
        public int? DestinationId { get; set; }

        [BindProperty]
        public DateTime? StartDate { get; set; }

        [BindProperty]
        public DateTime? EndDate { get; set; }

        [BindProperty]
        public decimal? Budget { get; set; }

        public string Destination { get; set; } = string.Empty;
        public List<DestinationOptionVm> Destinations { get; set; } = new();
        public int TotalDays { get; set; }
        public string? PlanSummary { get; set; }
        public List<DayPlanItem> DailyPlan { get; set; } = new();
        public BudgetBreakdownItem BudgetBreakdown { get; set; } = new();
        public bool SavedToMyPlans { get; set; }
        public string? SaveErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadDestinationsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDestinationsAsync();

            if (!DestinationId.HasValue || DestinationId <= 0)
            {
                ModelState.AddModelError(nameof(DestinationId), "Destinasyon seçimi zorunludur.");
                return Page();
            }

            if (!StartDate.HasValue)
            {
                ModelState.AddModelError(nameof(StartDate), "Başlangıç tarihi zorunludur.");
                return Page();
            }

            if (!EndDate.HasValue)
            {
                ModelState.AddModelError(nameof(EndDate), "Bitiş tarihi zorunludur.");
                return Page();
            }

            if (StartDate.HasValue && EndDate.HasValue && EndDate < StartDate)
            {
                ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıç tarihinden önce olamaz.");
                return Page();
            }

            if ((Budget ?? 0) <= 0)
            {
                ModelState.AddModelError(nameof(Budget), "Bütçe 0'dan büyük olmalıdır.");
                return Page();
            }

            var selectedDestination = await _db.Destinations!
                .FirstOrDefaultAsync(d => d.Id == DestinationId.Value);

            if (selectedDestination == null)
            {
                ModelState.AddModelError(nameof(DestinationId), "Geçerli bir destinasyon seçiniz.");
                return Page();
            }

            Destination = selectedDestination.Name;

            var start = StartDate!.Value.Date;
            var end = EndDate!.Value.Date;
            TotalDays = (end - start).Days + 1;

            DailyPlan = BuildDailyPlan(start, end, Destination);
            BudgetBreakdown = BuildBudgetBreakdown(Budget!.Value);

            PlanSummary = $"{Destination} için {TotalDays} günlük plan oluşturuldu.";

            if (User?.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    try
                    {
                        var itinerary = new Itinerary
                        {
                            Title = $"{Destination} Planı ({start:dd.MM.yyyy} - {end:dd.MM.yyyy})",
                            StartDate = start,
                            EndDate = end,
                            UserId = userId,
                            User = null!
                        };

                        itinerary.Destinations.Add(selectedDestination);

                        _db.Itineraries!.Add(itinerary);
                        await _db.SaveChangesAsync();
                        SavedToMyPlans = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Seyahat planı kaydedilemedi. UserId: {UserId}, DestinationId: {DestinationId}", userId, DestinationId);
                        SaveErrorMessage = "Plan oluşturuldu ancak veritabanına kaydedilemedi. Lütfen veritabanı/migration durumunu kontrol edin.";
                    }
                }
            }

            return Page();
        }

        private async Task LoadDestinationsAsync()
        {
            Destinations = await _db.Destinations!
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DestinationOptionVm { Id = d.Id, Name = d.Name })
                .ToListAsync();
        }

        private static List<DayPlanItem> BuildDailyPlan(DateTime start, DateTime end, string destination)
        {
            var items = new List<DayPlanItem>();
            var date = start;
            var day = 1;

            while (date <= end)
            {
                items.Add(new DayPlanItem
                {
                    DayNumber = day,
                    Date = date,
                    Morning = $"{destination} için merkezde kısa keşif ve kahvaltı",
                    Afternoon = "Öne çıkan noktaları gezme",
                    Evening = "Yerel lezzetler ve dinlenme"
                });

                date = date.AddDays(1);
                day++;
            }

            return items;
        }

        private static BudgetBreakdownItem BuildBudgetBreakdown(decimal totalBudget)
        {
            return new BudgetBreakdownItem
            {
                Accommodation = Math.Round(totalBudget * 0.40m, 2),
                Transport = Math.Round(totalBudget * 0.25m, 2),
                Food = Math.Round(totalBudget * 0.20m, 2),
                Activities = Math.Round(totalBudget * 0.15m, 2)
            };
        }

        public class DayPlanItem
        {
            public int DayNumber { get; set; }
            public DateTime Date { get; set; }
            public string Morning { get; set; } = string.Empty;
            public string Afternoon { get; set; } = string.Empty;
            public string Evening { get; set; } = string.Empty;
        }

        public class BudgetBreakdownItem
        {
            public decimal Accommodation { get; set; }
            public decimal Transport { get; set; }
            public decimal Food { get; set; }
            public decimal Activities { get; set; }
        }

        public class DestinationOptionVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}