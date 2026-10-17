using System.Threading.Tasks;

namespace Roma.Services
{
    public interface IEmailQueue
    {
        ValueTask EnqueueAsync(EmailMessage message);
    }
}
