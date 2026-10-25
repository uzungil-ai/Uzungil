using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using Roma.Services;
using Microsoft.Extensions.Logging;

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

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var isAjax = HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (string.IsNullOrWhiteSpace(Email) || !Consent)
            {
                ModelState.AddModelError("", "E-posta ve rıza gereklidir.");
                if (isAjax)
                {
                    return BadRequest(new { success = false, message = "E-posta ve rıza gereklidir." });
                }
                return Page();
            }

            var existing = await _db.Subscribers!.FirstOrDefaultAsync(s => s.Email == Email);
            if (existing != null)
            {
                var resentConfirmation = false;
                if (!existing.IsConfirmed)
                {
                    existing.ConsentGiven = true;
                    var token = System.Guid.NewGuid().ToString("N");
                    existing.ConfirmationToken = token;
                    existing.ConfirmationExpiresAt = System.DateTime.UtcNow.AddDays(7);
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
                    return new JsonResult(new { success = true, message = resentConfirmation ? "Bu e-posta zaten kayıtlı. Onay e-postasını tekrar gönderdik." : "Bu e-posta zaten onaylı şekilde kayıtlı." });
                }

                return RedirectToPage("/Index");
            }

            var tokenNew = System.Guid.NewGuid().ToString("N");
            var expires = System.DateTime.UtcNow.AddDays(7);
            var sub = new Subscriber { Email = Email, ConsentGiven = true, IsConfirmed = false, CreatedAt = System.DateTime.UtcNow, ConfirmationToken = tokenNew, ConfirmationExpiresAt = expires };
            _db.Subscribers!.Add(sub);
            await _db.SaveChangesAsync();

            var queued = await TryQueueConfirmationEmailAsync(Email, tokenNew);
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
                return new JsonResult(new { success = true, message = "Abonelik isteğiniz alındı. Onay için e-postanızı kontrol edin." });
            }

            return RedirectToPage("/Index");
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

            var body = $"Lütfen aboneliğinizi onaylamak için şu linke tıklayın (7 gün içinde geçerlidir): {link}";
            await _emailQueue.EnqueueAsync(new EmailMessage(email, "Abonelik Onayı", body));
            return true;
        }
    }
}
