using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;

namespace Roma.Services
{
    public class EmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailSenderService> _logger;
        private readonly string _host;
        private readonly int _port;
        private readonly string? _user;
        private readonly string? _pass;
        private readonly string _from;
        private readonly string? _fromName;
        private readonly bool _useSsl;

        public EmailSenderService(IConfiguration config, ILogger<EmailSenderService> logger)
        {
            _config = config;
            _logger = logger;
            _host = _config["Email:SmtpHost"] ?? _config["Smtp:Host"] ?? "";
            _port = int.TryParse(_config["Email:SmtpPort"], out var emailPort)
                ? emailPort
                : (int.TryParse(_config["Smtp:Port"], out var smtpPort) ? smtpPort : 25);
            _user = _config["Email:Username"] ?? _config["Smtp:User"];
            _pass = _config["Email:Password"] ?? _config["Smtp:Pass"];
            _from = _config["Email:FromAddress"] ?? _config["Smtp:From"] ?? _user ?? "noreply@example.com";
            _fromName = _config["Email:FromName"];
            _useSsl = bool.TryParse(_config["Email:EnableSsl"], out var emailSsl)
                ? emailSsl
                : (bool.TryParse(_config["Smtp:UseSsl"], out var smtpSsl) ? smtpSsl : !string.IsNullOrEmpty(_user));
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_host))
                {
                    throw new InvalidOperationException("SMTP host yapılandırılmamış. appsettings içinde Email:SmtpHost (veya Smtp:Host) değerini ayarlayın.");
                }

                using var msg = new MailMessage();
                msg.From = string.IsNullOrWhiteSpace(_fromName)
                    ? new MailAddress(_from)
                    : new MailAddress(_from, _fromName);
                msg.To.Add(new MailAddress(to));
                msg.Subject = subject;
                msg.Body = body;
                msg.IsBodyHtml = true;

                using var client = new SmtpClient(_host, _port);
                if (!string.IsNullOrEmpty(_user))
                {
                    client.Credentials = new NetworkCredential(_user, _pass);
                }
                client.EnableSsl = _useSsl;

                await client.SendMailAsync(msg);
                _logger.LogInformation("Email sent to {Email}", to);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", to);
                throw;
            }
        }
    }
}
