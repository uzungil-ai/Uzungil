using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;

namespace Roma.Services
{
    public class BackgroundEmailSender : BackgroundService
    {
        private readonly Channel<EmailMessage> _channel;
        private readonly IEmailSenderService _emailSender;
        private readonly ILogger<BackgroundEmailSender> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public BackgroundEmailSender(Channel<EmailMessage> channel, IEmailSenderService emailSender, ILogger<BackgroundEmailSender> logger, IServiceScopeFactory scopeFactory)
        {
            _channel = channel;
            _emailSender = emailSender;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var msg in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                var attempt = 0;
                var success = false;
                while (attempt < 3 && !success && !stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await _emailSender.SendEmailAsync(msg.To, msg.Subject, msg.Body);
                        success = true;
                    }
                    catch (System.Exception ex)
                    {
                        attempt++;
                        _logger.LogWarning(ex, "Failed to send email to {Email} on attempt {Attempt}", msg.To, attempt);
                        // exponential backoff
                        await Task.Delay(500 * (int)System.Math.Pow(2, attempt), stoppingToken).ContinueWith(_ => { });
                    }
                }

                if (!success)
                {
                    _logger.LogError("Giving up sending email to {Email} after retries", msg.To);
                    // persist to FailedEmails DLQ
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<Roma.Data.ApplicationDbContext>();
                        var failed = new Roma.Models.FailedEmail
                        {
                            To = msg.To,
                            Subject = msg.Subject,
                            Body = msg.Body,
                            Attempts = attempt,
                            LastError = "Delivery failed after retries",
                            CreatedAt = System.DateTime.UtcNow
                        };
                        db.FailedEmails!.Add(failed);
                        db.SaveChanges();
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist failed email to DLQ");
                    }
                }
            }
        }
    }
}
