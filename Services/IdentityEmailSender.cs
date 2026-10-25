using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace Roma.Services
{
    public class IdentityEmailSender : IEmailSender
    {
        private readonly IEmailQueue? _queue;
        private readonly IEmailSenderService _sender;
        private readonly ILogger<IdentityEmailSender> _logger;

        public IdentityEmailSender(IEmailSenderService sender, ILogger<IdentityEmailSender> logger, IEmailQueue? queue = null)
        {
            _sender = sender;
            _logger = logger;
            _queue = queue;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (_queue != null)
            {
                await _queue.EnqueueAsync(new EmailMessage(email, subject, htmlMessage));
                _logger.LogInformation("Identity email queued for {Email}", email);
                return;
            }

            await _sender.SendEmailAsync(email, subject, htmlMessage);
        }
    }
}
