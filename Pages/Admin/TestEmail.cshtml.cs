using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Services;

namespace Roma.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TestEmailModel : PageModel
    {
        private readonly IEmailSenderService _sender;
        private readonly IEmailQueue? _queue;

        public TestEmailModel(IEmailSenderService sender, IEmailQueue? queue = null)
        {
            _sender = sender;
            _queue = queue;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? StatusMessage { get; set; }

        public class InputModel
        {
            public string To { get; set; } = string.Empty;
            public string Subject { get; set; } = "SMTP Test";
            public string Body { get; set; } = "Bu bir test e-postasıdır.";
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.To))
            {
                StatusMessage = "Alıcı e-posta zorunludur.";
                return Page();
            }

            try
            {
                if (_queue != null)
                {
                    await _queue.EnqueueAsync(new EmailMessage(Input.To.Trim(), Input.Subject ?? "SMTP Test", Input.Body ?? string.Empty));
                    StatusMessage = "E-posta kuyruğa alındı. Kısa süre içinde gönderilecektir.";
                    return Page();
                }

                await _sender.SendEmailAsync(Input.To.Trim(), Input.Subject ?? "SMTP Test", Input.Body ?? string.Empty);
                StatusMessage = "Test e-postası gönderildi.";
                return Page();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Gönderim hatası: {ex.Message}";
                return Page();
            }
        }
    }
}
