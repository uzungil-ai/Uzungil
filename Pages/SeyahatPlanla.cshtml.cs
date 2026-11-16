using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using System.Net;
using System.Text;

namespace Roma.Pages
{
    public class SeyahatPlanlaModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SeyahatPlanlaModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public int? DestinationId { get; set; }

        [BindProperty]
        public DateTime? StartDate { get; set; }

        [BindProperty]
        public DateTime? EndDate { get; set; }

        [BindProperty]
        public decimal? Budget { get; set; }

        [BindProperty]
        public List<int> SelectedAttractionIds { get; set; } = new();

        [BindProperty]
        public List<int> SelectedFlavorIds { get; set; } = new();

        public string Destination { get; set; } = string.Empty;
        public List<DestinationOptionVm> Destinations { get; set; } = new();
        public int TotalDays { get; set; }
        public string? PlanSummary { get; set; }
        public List<DayPlanItem> DailyPlan { get; set; } = new();
        public BudgetBreakdownItem BudgetBreakdown { get; set; } = new();
        public bool SavedToMyPlans { get; set; }
        public string? SaveErrorMessage { get; set; }
        public List<SelectableItemVm> AllAttractions { get; set; } = new();
        public List<SelectableItemVm> AllFlavors { get; set; } = new();
        public List<string> SelectedAttractionNames { get; set; } = new();
        public List<string> SelectedFlavorNames { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDestinationsAsync();
            await LoadSelectableOptionsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDestinationsAsync();
            await LoadSelectableOptionsAsync();

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

            if (StartDate.HasValue && EndDate.HasValue)
            {
                var totalDays = (EndDate.Value.Date - StartDate.Value.Date).Days + 1;
                if (totalDays > 30)
                {
                    TempData["TravelPlanWarning"] = "Seyahat süresi 30 günü aşamaz.";

                    var referer = Request.Headers.Referer.ToString();
                    if (!string.IsNullOrWhiteSpace(referer)
                        && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
                    {
                        var localPath = refererUri.PathAndQuery;
                        if (Url.IsLocalUrl(localPath))
                        {
                            return Redirect(localPath);
                        }
                    }

                    return RedirectToPage();
                }
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

            SelectedAttractionNames = await _db.Attractions!
                .AsNoTracking()
                .Where(a => a.DestinationId == DestinationId.Value && SelectedAttractionIds.Contains(a.Id))
                .OrderBy(a => a.Name)
                .Select(a => a.Name)
                .ToListAsync();

            SelectedFlavorNames = await _db.Flavors!
                .AsNoTracking()
                .Where(f => f.DestinationId == DestinationId.Value && SelectedFlavorIds.Contains(f.Id))
                .OrderBy(f => f.Name)
                .Select(f => f.Name)
                .ToListAsync();

            var start = StartDate!.Value.Date;
            var end = EndDate!.Value.Date;
            TotalDays = (end - start).Days + 1;

            DailyPlan = BuildDailyPlan(start, end, Destination, SelectedAttractionNames, SelectedFlavorNames);
            BudgetBreakdown = BuildBudgetBreakdown(Budget!.Value);

            PlanSummary = $"{Destination} için {TotalDays} günlük plan oluşturuldu.";

            SavedToMyPlans = false;
            SaveErrorMessage = null;

            return Page();
        }

        public async Task<IActionResult> OnPostExportExcelAsync()
        {
            await LoadDestinationsAsync();
            await LoadSelectableOptionsAsync();

            if (!DestinationId.HasValue || DestinationId <= 0 || !StartDate.HasValue || !EndDate.HasValue || (Budget ?? 0) <= 0)
            {
                ModelState.AddModelError(string.Empty, "Excel çıktısı için geçerli plan bilgileri gereklidir.");
                return Page();
            }

            var start = StartDate.Value.Date;
            var end = EndDate.Value.Date;
            if (end < start)
            {
                ModelState.AddModelError(string.Empty, "Bitiş tarihi başlangıç tarihinden önce olamaz.");
                return Page();
            }

            var totalDays = (end - start).Days + 1;
            if (totalDays > 30)
            {
                TempData["TravelPlanWarning"] = "Seyahat süresi 30 günü aşamaz.";
                return RedirectToPage();
            }

            var selectedDestination = await _db.Destinations!
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == DestinationId.Value);

            if (selectedDestination == null)
            {
                ModelState.AddModelError(nameof(DestinationId), "Geçerli bir destinasyon seçiniz.");
                return Page();
            }

            var destinationName = selectedDestination.Name;
            var selectedAttractionNames = await _db.Attractions!
                .AsNoTracking()
                .Where(a => a.DestinationId == DestinationId.Value && SelectedAttractionIds.Contains(a.Id))
                .OrderBy(a => a.Name)
                .Select(a => a.Name)
                .ToListAsync();

            var selectedFlavorNames = await _db.Flavors!
                .AsNoTracking()
                .Where(f => f.DestinationId == DestinationId.Value && SelectedFlavorIds.Contains(f.Id))
                .OrderBy(f => f.Name)
                .Select(f => f.Name)
                .ToListAsync();

            var dailyPlan = BuildDailyPlan(start, end, destinationName, selectedAttractionNames, selectedFlavorNames);
            var budgetBreakdown = BuildBudgetBreakdown(Budget!.Value);

            var html = BuildExcelHtml(destinationName, start, end, totalDays, Budget.Value, budgetBreakdown, dailyPlan, selectedAttractionNames, selectedFlavorNames);
            var fileBytes = Encoding.UTF8.GetBytes(html);
            var safeName = ToSafeFileName(destinationName);
            var fileName = $"SeyahatPlani-{safeName}-{DateTime.Now:yyyyMMddHHmmss}.xls";

            return File(fileBytes, "application/vnd.ms-excel; charset=utf-8", fileName);
        }

        private async Task LoadDestinationsAsync()
        {
            Destinations = await _db.Destinations!
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .Select(d => new DestinationOptionVm { Id = d.Id, Name = d.Name })
                .ToListAsync();
        }

        private async Task LoadSelectableOptionsAsync()
        {
            AllAttractions = await _db.Attractions!
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .Select(a => new SelectableItemVm { Id = a.Id, Name = a.Name, DestinationId = a.DestinationId })
                .ToListAsync();

            AllFlavors = await _db.Flavors!
                .AsNoTracking()
                .OrderBy(f => f.Name)
                .Select(f => new SelectableItemVm { Id = f.Id, Name = f.Name, DestinationId = f.DestinationId })
                .ToListAsync();
        }

        private static List<DayPlanItem> BuildDailyPlan(
            DateTime start,
            DateTime end,
            string destination,
            List<string> selectedAttractions,
            List<string> selectedFlavors)
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
                    Afternoon = selectedAttractions.Count > 0
                        ? $"{selectedAttractions[(day - 1) % selectedAttractions.Count]} ziyaret"
                        : "Öne çıkan noktaları gezme",
                    Evening = selectedFlavors.Count > 0
                        ? $"{selectedFlavors[(day - 1) % selectedFlavors.Count]} tadımı ve dinlenme"
                        : "Yerel lezzetler ve dinlenme"
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

        private static string BuildExcelHtml(
            string destination,
            DateTime start,
            DateTime end,
            int totalDays,
            decimal totalBudget,
            BudgetBreakdownItem breakdown,
            List<DayPlanItem> dailyPlan,
            List<string> selectedAttractions,
            List<string> selectedFlavors)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8' /></head><body>");
            sb.AppendLine("<h2>Seyahat Planı</h2>");
            sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0'>");
            sb.AppendLine($"<tr><td><b>Destinasyon</b></td><td>{WebUtility.HtmlEncode(destination)}</td></tr>");
            sb.AppendLine($"<tr><td><b>Tarih</b></td><td>{start:dd.MM.yyyy} - {end:dd.MM.yyyy}</td></tr>");
            sb.AppendLine($"<tr><td><b>Toplam Gün</b></td><td>{totalDays}</td></tr>");
            sb.AppendLine($"<tr><td><b>Toplam Bütçe</b></td><td>{totalBudget:N0} ₺</td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<h3>Seçilen İçerikler</h3>");
            sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0'>");
            sb.AppendLine($"<tr><td>Cazibe Merkezleri</td><td>{WebUtility.HtmlEncode(selectedAttractions.Count > 0 ? string.Join(", ", selectedAttractions) : "-")}</td></tr>");
            sb.AppendLine($"<tr><td>Lezzetler</td><td>{WebUtility.HtmlEncode(selectedFlavors.Count > 0 ? string.Join(", ", selectedFlavors) : "-")}</td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<h3>Bütçe Dağılımı</h3>");
            sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0'>");
            sb.AppendLine($"<tr><td>Konaklama</td><td>{breakdown.Accommodation:N0} ₺</td></tr>");
            sb.AppendLine($"<tr><td>Ulaşım</td><td>{breakdown.Transport:N0} ₺</td></tr>");
            sb.AppendLine($"<tr><td>Yeme-İçme</td><td>{breakdown.Food:N0} ₺</td></tr>");
            sb.AppendLine($"<tr><td>Aktiviteler</td><td>{breakdown.Activities:N0} ₺</td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<h3>Günlük Taslak Plan</h3>");
            sb.AppendLine("<table border='1' cellpadding='6' cellspacing='0'>");
            sb.AppendLine("<tr><th>Gün</th><th>Tarih</th><th>Sabah</th><th>Öğle</th><th>Akşam</th></tr>");

            foreach (var day in dailyPlan)
            {
                sb.AppendLine($"<tr><td>{day.DayNumber}</td><td>{day.Date:dd.MM.yyyy}</td><td>{WebUtility.HtmlEncode(day.Morning)}</td><td>{WebUtility.HtmlEncode(day.Afternoon)}</td><td>{WebUtility.HtmlEncode(day.Evening)}</td></tr>");
            }

            sb.AppendLine("</table>");
            sb.AppendLine("</body></html>");

            return sb.ToString();
        }

        private static string ToSafeFileName(string value)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '-');
            }

            return string.IsNullOrWhiteSpace(value) ? "Plan" : value;
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

        public class SelectableItemVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int DestinationId { get; set; }
        }
    }
}