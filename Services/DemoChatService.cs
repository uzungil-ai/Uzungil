namespace Roma.Services
{
    public class DemoChatService : IChatService
    {
        public int Id { get; set; }
        public string Input { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string Reply { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Task<string> GetReplyAsync(string input, string? sessionId = null)
        {
            // echo demo
            return Task.FromResult($"(Bot) Siz: {input}. Ben de: Bu bir demo yanıttır.");
        }
    }
}
