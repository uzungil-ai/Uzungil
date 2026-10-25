using Microsoft.EntityFrameworkCore;
using Roma.Data;

namespace Roma.Services
{
    public class DatabaseChatService : IChatService
    {
        private readonly ApplicationDbContext _context;

        public DatabaseChatService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetReplyAsync(string input, string? sessionId = null)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "Lütfen bir mesaj yazın.";
            }

            var normalizedInput = input.Trim();
            var normalizedInputLower = normalizedInput.ToLower();

            var baseQuery = _context.DemoChatServices!
                .AsNoTracking()
                .Where(x => !string.IsNullOrWhiteSpace(x.Input) && !string.IsNullOrWhiteSpace(x.Reply));

            var exactReply = await baseQuery
                .Where(x => x.Input != null && x.Input.Trim().ToLower() == normalizedInputLower)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.Reply)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(exactReply))
            {
                return exactReply;
            }

            var containsReply = await baseQuery
                .Where(x => x.Input != null && EF.Functions.Like(normalizedInputLower, "%" + x.Input.Trim().ToLower() + "%"))
                .OrderByDescending(x => x.Input!.Length)
                .Take(10)
                .Select(x => x.Reply)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(containsReply))
            {
                return containsReply;
            }

            return "(Bot) Bu mesaj için kayıtlı bir yanıt bulunamadı.";
        }
    }
}
