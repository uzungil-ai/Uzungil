namespace Roma.Services
{
    public interface IOpenAiQaService
    {
        Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
    }
}