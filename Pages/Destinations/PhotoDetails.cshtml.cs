using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Roma.Services;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Destinations
{
    [AllowAnonymous]
    public class PhotoDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly HashSet<int> _subscriberDiscountMealIds;
        private readonly IConfiguration _configuration;
        private readonly IEmailQueue? _emailQueue;
        private readonly IEmailSenderService _emailSender;
        private readonly IMemoryCache _memoryCache;
        private readonly Dictionary<string, string> _srcSetCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _smallestImageCache = new(StringComparer.OrdinalIgnoreCase);

        public PhotoDetailsModel(ApplicationDbContext db, IWebHostEnvironment env, IConfiguration configuration, IEmailSenderService emailSender, IMemoryCache memoryCache, IEmailQueue? emailQueue = null)
        {
            _db = db;
            _env = env;
            _configuration = configuration;
            _emailSender = emailSender;
            _memoryCache = memoryCache;
            _emailQueue = emailQueue;
            _subscriberDiscountMealIds = configuration.GetSection("Discounts:MealIds").Get<int[]>()?.ToHashSet() ?? new HashSet<int>();
        }

        public Destination? Destination { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Destination = await _db.Destinations!
                .AsNoTracking()
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                    .ThenInclude(a => a.Reviews)
                        .ThenInclude(r => r.User)
                .Include(d => d.Hotels)
                .Include(d => d.Meals)
                .Include(d => d.Flavors)
                .Include(d => d.Parkings)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (Destination == null)
            {
                return NotFound();
            }

            return Page();
        }

        public string BuildSrcSet(string? imageUrl)
        {
            var smallestImageUrl = GetSmallestImageUrl(imageUrl);
            if (string.IsNullOrEmpty(smallestImageUrl)) return string.Empty;

            if (_srcSetCache.TryGetValue(smallestImageUrl, out var cachedSrcSet))
            {
                return cachedSrcSet;
            }

            var srcSet = $"{smallestImageUrl} 320w";
            _srcSetCache[smallestImageUrl] = srcSet;
            return srcSet;
        }

        public bool HasSubscriberDiscount(int mealId)
        {
            return _subscriberDiscountMealIds.Contains(mealId);
        }

        public async Task<IActionResult> OnPostSendCouponAsync(int id, int mealId, string email, string? website)
        {
            // Basit bot kontrolü (honeypot)
            if (!string.IsNullOrWhiteSpace(website))
            {
                TempData["CouponSuccess"] = "Kupon talebiniz alındı. E-postanızı kontrol edin.";
                return RedirectToPage(new { id });
            }

            if (mealId <= 0 || string.IsNullOrWhiteSpace(email))
            {
                TempData["CouponError"] = "Geçerli e-posta ve mekan seçimi gereklidir.";
                return RedirectToPage(new { id });
            }

            var normalizedEmail = email.Trim().ToLowerInvariant();
            if (!IsValidEmail(normalizedEmail))
            {
                TempData["CouponError"] = "Geçerli bir e-posta giriniz.";
                return RedirectToPage(new { id });
            }

            var subscriber = await _db.Subscribers!
                .FirstOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail);

            if (subscriber == null || !subscriber.IsConfirmed || !subscriber.ConsentGiven)
            {
                TempData["CouponError"] = "Bu e-posta onaylı abone değil. Önce aboneliği onaylayın.";
                return RedirectToPage(new { id });
            }

            // Basit rate-limit: aynı abone + aynı mekan için 10 dakikada bir talep
            var rateKey = $"coupon:{subscriber.Id}:{mealId}";
            if (_memoryCache.TryGetValue(rateKey, out _))
            {
                TempData["CouponError"] = "Bu mekan için kupon talebini kısa süre önce gönderdiniz. Lütfen 10 dakika sonra tekrar deneyin.";
                return RedirectToPage(new { id });
            }

            var meal = await _db.Meals!
                .AsNoTracking()
                .Include(x => x.Destination)
                .FirstOrDefaultAsync(x => x.Id == mealId && x.DestinationId == id);

            if (meal == null)
            {
                TempData["CouponError"] = "Restoran/cafe bulunamadı.";
                return RedirectToPage(new { id });
            }

            if (!HasSubscriberDiscount(mealId))
            {
                TempData["CouponError"] = "Bu restoran/cafe için kupon aktif değil.";
                return RedirectToPage(new { id });
            }

            if (string.IsNullOrWhiteSpace(subscriber.UnsubscribeToken))
            {
                subscriber.UnsubscribeToken = Guid.NewGuid().ToString("N");
                await _db.SaveChangesAsync();
            }

            var couponCode = _configuration["Discounts:CouponCode"];
            if (string.IsNullOrWhiteSpace(couponCode)) couponCode = "FOTO10";

            var percent = _configuration.GetValue<int?>("Discounts:Percent") ?? 10;
            if (percent <= 0) percent = 10;

            var subject = "İndirim Kuponu - Fotografia";
            var placeQuery = $"{meal.Name} {meal.Address} {meal.Destination?.Name}";
            var mapsUrl = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(placeQuery)}";
            var baseUrl = _configuration["App:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            var unsubscribeLink = string.IsNullOrWhiteSpace(baseUrl)
                ? $"/Unsubscribe?email={Uri.EscapeDataString(subscriber.Email)}&token={Uri.EscapeDataString(subscriber.UnsubscribeToken)}"
                : $"{baseUrl}/Unsubscribe?email={Uri.EscapeDataString(subscriber.Email)}&token={Uri.EscapeDataString(subscriber.UnsubscribeToken)}";

            var body = $"<html><body style='font-family:Arial,sans-serif;'>" +
                       $"<h2>Kupon kodunuz hazır</h2>" +
                       $"<p><strong>Kupon:</strong> {System.Net.WebUtility.HtmlEncode(couponCode)}</p>" +
                       $"<p><strong>İndirim:</strong> %{percent}</p>" +
                       $"<p><strong>Mekan:</strong> {System.Net.WebUtility.HtmlEncode(meal.Name)}</p>" +
                       (string.IsNullOrWhiteSpace(meal.Address) ? string.Empty : $"<p><strong>Adres:</strong> {System.Net.WebUtility.HtmlEncode(meal.Address)}</p>") +
                       $"<p><a href='{mapsUrl}'>Haritada aç</a></p>" +
                       $"<p style='margin-top:16px;font-size:12px;color:#666;'>E-posta almak istemiyorsanız <a href='{unsubscribeLink}'>abonelikten çıkın</a>.</p>" +
                       "</body></html>";

            if (_emailQueue != null)
            {
                await _emailQueue.EnqueueAsync(new EmailMessage(subscriber.Email, subject, body));
            }
            else
            {
                await _emailSender.SendEmailAsync(subscriber.Email, subject, body);
            }

            _memoryCache.Set(rateKey, true, TimeSpan.FromMinutes(10));

            TempData["CouponSuccess"] = "İndirim kuponu e-posta adresinize gönderildi.";
            return RedirectToPage(new { id });
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public string GetSmallestImageUrl(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return string.Empty;

            if (_smallestImageCache.TryGetValue(imageUrl, out var cachedImageUrl))
            {
                return cachedImageUrl;
            }

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var rel = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.Combine(webRoot, rel);
                var dir = Path.GetDirectoryName(full) ?? webRoot;
                var name = Path.GetFileNameWithoutExtension(full);

                var baseName = name;
                var sizeSuffixes = new[] { "-320", "-640", "-1024" };
                foreach (var suffix in sizeSuffixes)
                {
                    if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^suffix.Length];
                        break;
                    }
                }

                var candidates = new[]
                {
                    Path.Combine(dir, baseName + "-320.webp"),
                    Path.Combine(dir, baseName + "-640.webp"),
                    Path.Combine(dir, baseName + "-1024.webp"),
                    Path.Combine(dir, baseName + ".webp")
                };

                foreach (var candidate in candidates)
                {
                    if (System.IO.File.Exists(candidate))
                    {
                        var resolvedCandidate = "/" + Path.GetRelativePath(webRoot, candidate).Replace('\\', '/');
                        _smallestImageCache[imageUrl] = resolvedCandidate;
                        return resolvedCandidate;
                    }
                }

                if (System.IO.File.Exists(full))
                {
                    var resolvedFull = "/" + Path.GetRelativePath(webRoot, full).Replace('\\', '/');
                    _smallestImageCache[imageUrl] = resolvedFull;
                    return resolvedFull;
                }

                _smallestImageCache[imageUrl] = imageUrl;
                return imageUrl;
            }
            catch
            {
                _smallestImageCache[imageUrl] = imageUrl;
                return imageUrl;
            }
        }
    }
}
