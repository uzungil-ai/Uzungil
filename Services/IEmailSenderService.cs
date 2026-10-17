using System.Threading.Tasks;

namespace Roma.Services
{
    public interface IEmailSenderService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
