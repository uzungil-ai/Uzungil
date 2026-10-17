namespace Roma.Services
{
    public class DemoVisionService : IVisionService
    {
        public Task<string> GenerateAltTextAsync(Stream imageStream)
        {
            // Simple demo: return a static description
            return Task.FromResult("Resim: güzel bir destinasyon fotoğrafı.");
        }
    }
}
