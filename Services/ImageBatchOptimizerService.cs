using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Roma.Services
{
    public sealed class ImageBatchOptimizerService
    {
        private static readonly string[] SourceExtensions = [".png", ".jpg", ".jpeg"];
        private static readonly int[] TargetSizes = [320, 640, 1024];

        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ImageBatchOptimizerService> _logger;

        public ImageBatchOptimizerService(IWebHostEnvironment env, ILogger<ImageBatchOptimizerService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public sealed class BatchResult
        {
            public int Requested { get; set; }
            public int ProcessedFiles { get; set; }
            public int ConvertedVariants { get; set; }
            public int SkippedVariants { get; set; }
            public int FailedFiles { get; set; }
            public string Message { get; set; } = string.Empty;
            public int RemainingFilesEstimate { get; set; }
        }

        public async Task<BatchResult> OptimizeUploadsBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var result = new BatchResult { Requested = Math.Max(1, batchSize) };

            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(_env.ContentRootPath, "wwwroot")
                : _env.WebRootPath;

            var uploadsRoot = Path.Combine(webRoot, "uploads");
            if (!Directory.Exists(uploadsRoot))
            {
                result.Message = "uploads klasörü bulunamadı.";
                return result;
            }

            var candidates = Directory
                .EnumerateFiles(uploadsRoot, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedSource)
                .Where(NeedsAnyVariant)
                .Take(result.Requested)
                .ToList();

            if (candidates.Count == 0)
            {
                result.Message = "Dönüştürülecek dosya bulunamadı.";
                return result;
            }

            foreach (var sourcePath in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.ProcessedFiles++;

                try
                {
                    var basePath = Path.Combine(
                        Path.GetDirectoryName(sourcePath) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(sourcePath));

                    foreach (var size in TargetSizes)
                    {
                        var outputPath = $"{basePath}-{size}.webp";
                        if (File.Exists(outputPath))
                        {
                            result.SkippedVariants++;
                            continue;
                        }

                        var ok = await ConvertWithImageSharpAsync(sourcePath, outputPath, size, cancellationToken);
                        if (ok)
                        {
                            result.ConvertedVariants++;
                        }
                        else
                        {
                            result.FailedFiles++;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedFiles++;
                    _logger.LogError(ex, "Toplu görsel optimizasyonunda hata. File: {File}", sourcePath);
                }
            }

            result.RemainingFilesEstimate = Directory
                .EnumerateFiles(uploadsRoot, "*.*", SearchOption.AllDirectories)
                .Count(path => IsSupportedSource(path) && NeedsAnyVariant(path));

            result.Message = "İşlem tamamlandı.";
            return result;
        }

        private static bool IsSupportedSource(string path)
        {
            var ext = Path.GetExtension(path);
            return SourceExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static bool NeedsAnyVariant(string sourcePath)
        {
            var basePath = Path.Combine(
                Path.GetDirectoryName(sourcePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(sourcePath));

            return TargetSizes.Any(size => !File.Exists($"{basePath}-{size}.webp"));
        }

        private async Task<bool> ConvertWithImageSharpAsync(string inputPath, string outputPath, int size, CancellationToken cancellationToken)
        {
            try
            {
                using var image = await Image.LoadAsync(inputPath, cancellationToken);

                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(size, size),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                }));

                var encoder = new WebpEncoder
                {
                    Quality = 82,
                    FileFormat = WebpFileFormatType.Lossy,
                    Method = WebpEncodingMethod.BestQuality
                };

                await image.SaveAsync(outputPath, encoder, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImageSharp dönüşüm hatası. Input: {Input}", inputPath);
                return false;
            }
        }
    }
}
