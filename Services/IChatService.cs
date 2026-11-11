namespace Roma.Services
{
    public interface IChatService
    {
        Task<string> GetReplyAsync(string input, string? sessionId = null, CancellationToken cancellationToken = default);
    }
}
