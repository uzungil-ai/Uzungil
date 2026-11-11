namespace Roma.Services
{
    public class ImageProcessingService
    {
        public ImageProcessingService(IWebHostEnvironment env)
        {
            // Lisans gerektiren image processing kaldırıldı.
            // DI uyumluluğu için ctor korunuyor.
        }

        public async Task<string> SaveResizedAsync(Stream inputStream, string destFolder, string fileNameWithoutExt)
        {
            Directory.CreateDirectory(destFolder);

            // Çağıran kod orijinal dosyayı önce diske yazıyor.
            // Burada aynı dosyanın yolunu döndürerek akışı koruyoruz.
            var existing = Directory
                .EnumerateFiles(destFolder, $"{fileNameWithoutExt}.*")
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            // Güvenli fallback: stream'i dosyaya kaydet.
            var fallbackPath = Path.Combine(destFolder, $"{fileNameWithoutExt}.img");
            if (inputStream.CanSeek)
            {
                inputStream.Position = 0;
            }

            await using (var outStream = File.Create(fallbackPath))
            {
                await inputStream.CopyToAsync(outStream);
            }

            return fallbackPath;
        }
    }
}
