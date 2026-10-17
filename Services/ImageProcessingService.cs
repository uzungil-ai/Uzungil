using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Roma.Services
{
    public class ImageProcessingService
    {
        private readonly IWebHostEnvironment _env;
        public ImageProcessingService(IWebHostEnvironment env) => _env = env;

        public async Task<string> SaveResizedAsync(Stream inputStream, string destFolder, string fileNameWithoutExt)
        {
            Directory.CreateDirectory(destFolder);
            // generate multiple sizes and webp
            var sizes = new[] { 320, 640, 1024 };
            var savedFiles = new List<string>();

            using (var image = await Image.LoadAsync(inputStream))
            {
                foreach (var w in sizes)
                {
                    var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(w, 0) }));
                    var outName = $"{fileNameWithoutExt}-{w}.webp";
                    var outPath = Path.Combine(destFolder, outName);
                    await clone.SaveAsync(outPath, new WebpEncoder { Quality = 75 });
                    savedFiles.Add(outPath);
                }
            }

            // return smallest webp relative path
            var rel = savedFiles.OrderBy(p => new FileInfo(p).Length).First();
            return rel;
        }
    }
}
