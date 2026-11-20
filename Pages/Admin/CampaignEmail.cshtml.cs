using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using Roma.Services;

namespace Roma.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CampaignEmailModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IEmailQueue? _queue;
        private readonly IEmailSenderService _emailSender;

        public CampaignEmailModel(ApplicationDbContext db, IConfiguration configuration, IEmailSenderService emailSender, IEmailQueue? queue = null)
        {
            _db = db;
            _configuration = configuration;
            _emailSender = emailSender;
            _queue = queue;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? StatusMessage { get; set; }
        public List<Meal> PreviewMeals { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Kupon kodu zorunludur.")]
            public string CouponCode { get; set; } = "FOTO10";

            [Range(1, 100, ErrorMessage = "İndirim yüzdesi 1-100 arasında olmalıdır.")]
            public int DiscountPercent { get; set; } = 10;

            [Required(ErrorMessage = "En az bir restoran/cafe ID giriniz.")]
            public string MealIdsCsv { get; set; } = string.Empty;

            [Required(ErrorMessage = "Kampanya konusu zorunludur.")]
            public string Subject { get; set; } = "Fotografia abonelerine özel indirim";

            public string? ExtraMessage { get; set; }
        }

        public async Task OnGetAsync()
        {
            var ids = _configuration.GetSection("Discounts:MealIds").Get<int[]>() ?? Array.Empty<int>();
            Input.MealIdsCsv = string.Join(",", ids);
            PreviewMeals = await LoadMealsAsync(ids);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var ids = ParseMealIds(Input.MealIdsCsv);
            if (ids.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Geçerli restoran/cafe ID listesi giriniz.");
                return Page();
            }

            var meals = await LoadMealsAsync(ids.ToArray());
            if (meals.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Girilen ID'lerle eşleşen restoran/cafe bulunamadı.");
                return Page();
            }

            var subscribers = await _db.Subscribers!
                .Where(s => s.IsConfirmed && s.ConsentGiven)
                .OrderBy(s => s.Email)
                .ToListAsync();

            if (subscribers.Count == 0)
            {
                StatusMessage = "Onaylı abone bulunamadı.";
                return Page();
            }

            var tokenUpdated = false;
            foreach (var sub in subscribers)
            {
                if (string.IsNullOrWhiteSpace(sub.UnsubscribeToken))
                {
                    sub.UnsubscribeToken = Guid.NewGuid().ToString("N");
                    tokenUpdated = true;
                }
            }

            if (tokenUpdated)
            {
                await _db.SaveChangesAsync();
            }

            var baseUrl = _configuration["App:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            var subject = string.IsNullOrWhiteSpace(Input.Subject)
                ? "Fotografia abonelerine özel indirim"
                : Input.Subject.Trim();

            var sentCount = 0;
            foreach (var sub in subscribers)
            {
                var body = BuildCampaignHtml(sub, meals, baseUrl);
                if (_queue != null)
                {
                    await _queue.EnqueueAsync(new EmailMessage(sub.Email, subject, body));
                }
                else
                {
                    await _emailSender.SendEmailAsync(sub.Email, subject, body);
                }
                sentCount++;
            }

            StatusMessage = _queue != null
                ? $"{sentCount} abone için kampanya e-postası kuyruğa alındı."
                : $"{sentCount} aboneye kampanya e-postası gönderildi.";

            PreviewMeals = meals;
            return Page();
        }

        private List<int> ParseMealIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new List<int>();
            }

            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private Task<List<Meal>> LoadMealsAsync(int[] mealIds)
        {
            return _db.Meals!
                .AsNoTracking()
                .Include(m => m.Destination)
                .Where(m => mealIds.Contains(m.Id))
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        private string BuildCampaignHtml(Subscriber sub, List<Meal> meals, string baseUrl)
        {
            var sb = new StringBuilder();
            sb.Append("<html><body style='font-family:Arial,sans-serif;'>");
            sb.Append($"<h2>Fotografia abonelerine özel %{Input.DiscountPercent} indirim</h2>");
            sb.Append($"<p><strong>Kupon kodu:</strong> {System.Net.WebUtility.HtmlEncode(Input.CouponCode.Trim())}</p>");

            if (!string.IsNullOrWhiteSpace(Input.ExtraMessage))
            {
                sb.Append($"<p>{System.Net.WebUtility.HtmlEncode(Input.ExtraMessage)}</p>");
            }

            sb.Append("<p>İndirim geçerli mekanlar:</p><ul>");
            foreach (var meal in meals)
            {
                var q = $"{meal.Name} {meal.Address} {meal.Destination?.Name}";
                var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(q)}";
                sb.Append("<li>");
                sb.Append($"<strong>{System.Net.WebUtility.HtmlEncode(meal.Name)}</strong>");
                if (!string.IsNullOrWhiteSpace(meal.Address))
                {
                    sb.Append($" - {System.Net.WebUtility.HtmlEncode(meal.Address)}");
                }
                sb.Append($" (<a href='{mapsUrl}'>Haritada aç</a>)");
                sb.Append("</li>");
            }
            sb.Append("</ul>");

            var unsubscribeLink = string.IsNullOrWhiteSpace(baseUrl)
                ? $"/Unsubscribe?email={Uri.EscapeDataString(sub.Email)}&token={Uri.EscapeDataString(sub.UnsubscribeToken ?? string.Empty)}"
                : $"{baseUrl}/Unsubscribe?email={Uri.EscapeDataString(sub.Email)}&token={Uri.EscapeDataString(sub.UnsubscribeToken ?? string.Empty)}";

            sb.Append($"<p style='margin-top:16px;font-size:12px;color:#666;'>E-posta almak istemiyorsanız <a href='{unsubscribeLink}'>abonelikten çıkın</a>.</p>");
            sb.Append("</body></html>");

            return sb.ToString();
        }
    }
}