namespace Roma.Services
{
    public interface IVisionService
    {
        Task<string> GenerateAltTextAsync(Stream imageStream);
    }
}
