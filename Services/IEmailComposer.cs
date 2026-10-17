namespace Roma.Services
{
    public interface IEmailComposer
    {
        Task<string> ComposeNewDestinationHtmlAsync(string destinationName, string description, string link);
    }
}
