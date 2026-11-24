using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Roma.Data;
using Roma.Models;
using Roma.Services;
using System.Net.Mail;

namespace Roma.Pages
{
    public class SubscribeModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailQueue? _emailQueue;
        private readonly ILogger<SubscribeModel> _logger;

        public SubscribeModel(ApplicationDbContext db, IEmailQueue? emailQueue, ILogger<SubscribeModel> logger)
        {
            _db = db;
            _emailQueue = emailQueue;
            _logger = logger;
        }

        [BindProperty]
        public string Email { get; set; } = null!;

        [BindProperty]
        public bool Consent { get; set; }

        public void OnGet(string? email = null)
        {
            Email = NormalizeEmail(email);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var isAjax = HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            var normalizedEmail = NormalizeEmail(Email);
            var mailboxUrl = BuildMailboxUrl(normalizedEmail);

            if (string.IsNullOrWhiteSpace(normalizedEmail) || !Consent)
            {
                ModelState.AddModelError("", "E-posta ve rıza gereklidir.");
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = "E-posta ve rıza gereklidir." });
                }

                return Page();
            }

            if (!IsValidEmail(normalizedEmail))
            {
                ModelState.AddModelError("", "Geçerli bir e-posta adresi giriniz.");
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = "Geçerli bir e-posta adresi giriniz." });
                }

                return Page();
            }

            var existing = await _db.Subscribers!.FirstOrDefaultAsync(s => s.Email.ToLower() == normalizedEmail.ToLower());
            if (existing != null)
            {
                var resentConfirmation = false;
                if (!existing.IsConfirmed)
                {
                    existing.ConsentGiven = true;
                    var token = Guid.NewGuid().ToString("N");
                    existing.ConfirmationToken = token;
                    existing.ConfirmationExpiresAt = DateTime.UtcNow.AddDays(7);
                    await _db.SaveChangesAsync();

                    var resent = await TryQueueConfirmationEmailAsync(existing.Email, token);
                    if (!resent)
                    {
                        _logger.LogWarning("Abonelik onay e-postası kuyruğa alınamadı (mevcut onaysız kayıt): {Email}", existing.Email);
                        if (isAjax)
                        {
                            return StatusCode(500, new { success = false, message = "Kayıt bulundu ancak onay e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin." });
                        }

                        ModelState.AddModelError("", "Kayıt bulundu ancak onay e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin.");
                        return Page();
                    }

                    resentConfirmation = true;
                }

                if (isAjax)
                {
                    return new JsonResult(new
                    {
                        success = true,
                        message = resentConfirmation
                            ? "Bu e-posta zaten kayıtlı. Onay bağlantısını tekrar gönderdik. Aboneliği tamamlamak için e-postanızı açıp onay vermeniz gerekiyor."
                            : "Bu e-posta zaten onaylı şekilde kayıtlı.",
                        openEmailUrl = resentConfirmation ? mailboxUrl : null
                    });
                }

                TempData["SubscribeAlert"] = resentConfirmation
                    ? "Onay e-postası tekrar gönderildi. Aboneliği tamamlamak için e-posta adresinizi açıp onay vermeniz gerekiyor."
                    : "Bu e-posta zaten onaylı abonedir. Yorum ekleyebilirsiniz.";
                TempData["SubscribeEmailOpenUrl"] = resentConfirmation ? mailboxUrl : null;
                return RedirectToPage("/Subscribe");
            }

            var tokenNew = Guid.NewGuid().ToString("N");
            var expires = DateTime.UtcNow.AddDays(7);
            var sub = new Subscriber
            {
                Email = normalizedEmail,
                ConsentGiven = true,
                IsConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                ConfirmationToken = tokenNew,
                ConfirmationExpiresAt = expires
            };

            _db.Subscribers!.Add(sub);
            await _db.SaveChangesAsync();

            var queued = await TryQueueConfirmationEmailAsync(normalizedEmail, tokenNew);
            if (!queued)
            {
                _logger.LogWarning("Abonelik onay e-postası kuyruğa alınamadı: {Email}", Email);
                if (isAjax)
                {
                    return StatusCode(500, new { success = false, message = "Abonelik kaydedildi ancak onay e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin." });
                }

                ModelState.AddModelError("", "Abonelik kaydedildi ancak onay e-postası gönderilemedi. Lütfen daha sonra tekrar deneyin.");
                return Page();
            }

            if (isAjax)
            {
                return new JsonResult(new
                {
                    success = true,
                    message = "Abonelik isteğiniz alındı. Onay bağlantısı e-posta adresinize gönderildi. Aboneliği tamamlamak için e-postanızı açıp onay vermeniz gerekiyor.",
                    openEmailUrl = mailboxUrl
                });
            }

            TempData["SubscribeAlert"] = "Onay bağlantısı e-posta adresinize gönderildi. Aboneliği tamamlamak için e-postanızı açıp onay vermeniz gerekiyor.";
            TempData["SubscribeEmailOpenUrl"] = mailboxUrl;
            return RedirectToPage("/Subscribe");
        }

        public async Task<IActionResult> OnPostUnsubscribeAsync()
        {
            var isAjax = HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            var normalizedEmail = NormalizeEmail(Email);

            if (string.IsNullOrWhiteSpace(normalizedEmail) || !IsValidEmail(normalizedEmail))
            {
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = "Geçerli bir e-posta adresi giriniz." });
                }

                ModelState.AddModelError("", "Geçerli bir e-posta adresi giriniz.");
                return Page();
            }

            var existing = await _db.Subscribers!.FirstOrDefaultAsync(s => s.Email.ToLower() == normalizedEmail.ToLower());
            if (existing != null)
            {
                existing.ConsentGiven = false;
                existing.IsConfirmed = false;
                await _db.SaveChangesAsync();
            }

            if (isAjax)
            {
                return new JsonResult(new { success = true, message = "Abonelik kaydınız silindi." });
            }

            TempData["SubscribeAlert"] = "Aboneliğiniz iptal edildi.";
            return RedirectToPage("/Subscribe");
        }

        private static string NormalizeEmail(string? email)
        {
            return (email ?? string.Empty).Trim();
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new MailAddress(email);
                return addr.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string BuildMailboxUrl(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                return "https://mail.google.com";
            }

            var domain = email.Split('@').LastOrDefault()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (domain.Contains("gmail.com"))
                return "https://mail.google.com";
            if (domain.Contains("outlook.") || domain.Contains("hotmail.") || domain.Contains("live."))
                return "https://outlook.live.com/mail/0/";
            if (domain.Contains("yahoo."))
                return "https://mail.yahoo.com";
            if (domain.Contains("yandex."))
                return "https://mail.yandex.com";

            return "https://mail.google.com";
        }

        private async Task<bool> TryQueueConfirmationEmailAsync(string email, string token)
        {
            if (_emailQueue == null)
            {
                _logger.LogError("IEmailQueue servisi bulunamadı; abonelik e-postası kuyruğa alınamadı.");
                return false;
            }

            var link = Url.Page(
                "/SubscribeConfirm",
                pageHandler: null,
                values: new { email, token },
                protocol: Request.Scheme);

            if (string.IsNullOrWhiteSpace(link))
            {
                _logger.LogError("SubscribeConfirm link'i üretilemedi: {Email}", email);
                return false;
            }

            var body =
                "Fotografia abonelik talebiniz alındı.\n\n" +
                "Aboneliği tamamlamak için e-postanızdaki onay bağlantısına tıklamanız gerekiyor.\n" +
                $"Onay bağlantısı (7 gün geçerli): {link}\n\n" +
                "Bu işlemi siz yapmadıysanız bu e-postayı yok sayabilirsiniz.";

            await _emailQueue.EnqueueAsync(new EmailMessage(email, "Abonelik Onayı", body));
            return true;
        }
    }
}