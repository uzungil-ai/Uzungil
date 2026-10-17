using System.Threading.Channels;
using System.Threading.Tasks;

namespace Roma.Services
{
    public class EmailQueue : IEmailQueue
    {
        private readonly Channel<EmailMessage> _channel;

        public EmailQueue(Channel<EmailMessage> channel)
        {
            _channel = channel;
        }

        public ValueTask EnqueueAsync(EmailMessage message)
        {
            var written = _channel.Writer.TryWrite(message);
            if (!written)
            {
                // fallback to async write
                return new ValueTask(_channel.Writer.WriteAsync(message).AsTask());
            }
            return ValueTask.CompletedTask;
        }
    }
}
