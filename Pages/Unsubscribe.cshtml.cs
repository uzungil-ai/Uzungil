using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Models;
using Microsoft.EntityFrameworkCore;

namespace Roma.Pages
{
    public class UnsubscribeModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly Roma.Services.IEmailQueue? _queue;
        private readonly IConfiguration _configuration;

        public UnsubscribeModel(ApplicationDbContext db, IConfiguration configuration, Roma.Services.IEmailQueue? queue = null)
        {
            _db = db;
            _queue = queue;
            _configuration = configuration;
        }

        [BindProperty]
        public string Email { get; set; } = null!;
        [BindProperty]
        public string Token { get; set; } = null!;

        public bool Success { get; set; }

        public void OnGet(string email, string token)
        {
            Email = email;
            Token = token;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Token)) return Page();

            var sub = await _db.Subscribers!.FirstOrDefaultAsync(s => s.Email == Email && s.UnsubscribeToken == Token);
            if (sub != null)
            {
                sub.ConsentGiven = false;
                await _db.SaveChangesAsync();
                Success = true;

                // enqueue confirmation email with feedback link
                try
                {
                    var baseUrl = _configuration["App:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
                    var feedbackLink = string.IsNullOrEmpty(baseUrl) ? "/Feedback" : $"{baseUrl}/Feedback";
                    var body = $"<html><body><p>Aboneliğiniz iptal edildi: {System.Net.WebUtility.HtmlEncode(sub.Email)}</p><p>Görüşünüzü paylaşmak isterseniz: <a href=\"{feedbackLink}\">geribildirim</a></p></body></html>";
                    if (_queue != null)
                    {
                        await _queue.EnqueueAsync(new Roma.Services.EmailMessage(sub.Email, "Abonelik iptal edildi", body));
                    }
                }
                catch
                {
                }
            }

            return Page();
        }
    }
}
