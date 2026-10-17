namespace Roma.Services
{
    public class DemoEmailComposer : IEmailComposer
    {
        public Task<string> ComposeNewDestinationHtmlAsync(string destinationName, string description, string link)
        {
            var html = $"<h1>Yeni destinasyon: {System.Net.WebUtility.HtmlEncode(destinationName)}</h1><p>{System.Net.WebUtility.HtmlEncode(description)}</p><p><a href=\"{link}\">Detayı görüntüle</a></p>";
            return Task.FromResult(html);
        }
    }
}
