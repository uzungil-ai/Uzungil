namespace Roma.Services
{
    public class DemoChatService : IChatService
    {
        public Task<string> GetReplyAsync(string input, string? sessionId = null)
        {
            // echo demo
            return Task.FromResult($"(Bot) Siz: {input}. Ben de: Bu bir demo yanıttır.");
        }
    }
}
