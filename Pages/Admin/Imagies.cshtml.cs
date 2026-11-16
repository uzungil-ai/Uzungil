using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;

namespace Roma.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ImagiesModel : PageModel
    {
        private const int PageSize = 12;
        private readonly IWebHostEnvironment _environment;
        private readonly ApplicationDbContext _db;

        public ImagiesModel(IWebHostEnvironment environment, ApplicationDbContext db)
        {
            _environment = environment;
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int? DestinationId { get; set; }

        public IReadOnlyList<WebpImageItem> Images { get; private set; } = Array.Empty<WebpImageItem>();

        public IReadOnlyList<DestinationOption> DestinationOptions { get; private set; } = Array.Empty<DestinationOption>();

        public int TotalCount { get; private set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        public async Task OnGetAsync()
        {
            var webRoot = _environment.WebRootPath;

            if (_db.Destinations != null)
            {
                DestinationOptions = await _db.Destinations
                    .OrderBy(x => x.Name)
                    .Select(x => new DestinationOption
                    {
                        Id = x.Id,
                        Name = x.Name
                    })
                    .ToListAsync();
            }
            else
            {
                DestinationOptions = Array.Empty<DestinationOption>();
            }

            if (string.IsNullOrWhiteSpace(webRoot) || !Directory.Exists(webRoot))
            {
                Images = Array.Empty<WebpImageItem>();
                TotalCount = 0;
                CurrentPage = 1;
                return;
            }

            var uploadsRoot = Path.Combine(webRoot, "uploads", "destinations");

            if (!Directory.Exists(uploadsRoot))
            {
                Images = Array.Empty<WebpImageItem>();
                TotalCount = 0;
                CurrentPage = 1;
                return;
            }

            IEnumerable<string> filePaths;

            if (DestinationId.HasValue)
            {
                var destinationDir = Path.Combine(uploadsRoot, DestinationId.Value.ToString());
                filePaths = Directory.Exists(destinationDir)
                    ? Directory.EnumerateFiles(destinationDir, "*.webp", SearchOption.AllDirectories)
                    : Enumerable.Empty<string>();
            }
            else
            {
                filePaths = Directory.EnumerateFiles(uploadsRoot, "*.webp", SearchOption.AllDirectories);
            }

            var allItems = filePaths
                .Select(filePath =>
                {
                    var fileInfo = new FileInfo(filePath);
                    var relativePath = Path.GetRelativePath(webRoot, filePath).Replace("\\", "/");

                    return new WebpImageItem
                    {
                        Name = fileInfo.Name,
                        SizeBytes = fileInfo.Length,
                        SizeText = FormatFileSize(fileInfo.Length),
                        Url = "/" + relativePath,
                        LastWriteUtc = fileInfo.LastWriteTimeUtc
                    };
                })
                .OrderBy(x => x.SizeBytes)
                .ThenByDescending(x => x.LastWriteUtc)
                .ThenBy(x => x.Name)
                .ToList();

            TotalCount = allItems.Count;
            CurrentPage = Math.Max(1, CurrentPage);
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            Images = allItems
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        private static string FormatFileSize(long bytes)
        {
            const double KB = 1024d;
            const double MB = KB * 1024d;

            if (bytes >= MB)
            {
                return $"{bytes / MB:0.##} MB";
            }

            if (bytes >= KB)
            {
                return $"{bytes / KB:0.##} KB";
            }

            return $"{bytes} B";
        }

        public sealed class WebpImageItem
        {
            public string Name { get; init; } = string.Empty;
            public long SizeBytes { get; init; }
            public string SizeText { get; init; } = string.Empty;
            public string Url { get; init; } = string.Empty;
            public DateTime LastWriteUtc { get; init; }
        }

        public sealed class DestinationOption
        {
            public int Id { get; init; }
            public string Name { get; init; } = string.Empty;
        }
    }
}