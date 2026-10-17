namespace Roma.Services
{
    public interface IAiService
    {
        Task<string> GenerateDescriptionAsync(string prompt);
    }
}
