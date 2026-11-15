using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly Dictionary<string, string> _srcSetCache = new(StringComparer.OrdinalIgnoreCase);

        public IndexModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<Destination> Featured { get; set; } = new List<Destination>();
        public List<Destination> Latest { get; set; } = new List<Destination>();
        public int TotalDestinationCount { get; set; }
        public int TotalPhotoCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Name { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Country { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Conutry { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Category { get; set; }

        public bool HasActiveSearch =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(Name) ||
            !string.IsNullOrWhiteSpace(Country) ||
            !string.IsNullOrWhiteSpace(Category);

        public async Task OnGetAsync()
        {
            TotalDestinationCount = await _db.Destinations!.CountAsync();
            TotalPhotoCount = await _db.DestinationImages!.CountAsync();

            Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
            Conutry = string.IsNullOrWhiteSpace(Conutry) ? null : Conutry.Trim();
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim();

            if (Country is null && Conutry is not null)
            {
                Country = Conutry;
            }

            var baseQuery = _db.Destinations!
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .AsNoTracking()
                .AsQueryable();

            if (Search is not null)
            {
                var pattern = $"%{Search}%";
                baseQuery = baseQuery.Where(d =>
                    (d.Name != null && EF.Functions.Like(d.Name, pattern)) ||
                    (d.Country != null && EF.Functions.Like(d.Country, pattern)));
            }

            if (Name is not null)
            {
                var pattern = $"%{Name}%";
                baseQuery = baseQuery.Where(d => d.Name != null && EF.Functions.Like(d.Name, pattern));
            }

            if (Country is not null)
            {
                var pattern = $"%{Country}%";
                baseQuery = baseQuery.Where(d => d.Country != null && EF.Functions.Like(d.Country, pattern));
            }

            if (Category is not null)
            {
                var pattern = $"%{Category}%";
                baseQuery = baseQuery.Where(d => d.Attractions.Any(a => a.Category != null && EF.Functions.Like(a.Category, pattern)));
            }

            Featured = await baseQuery
                .OrderBy(_ => Guid.NewGuid())
                .Take(24)
                .ToListAsync();

            Latest = await baseQuery
                .OrderByDescending(d => d.Id)
                .Take(24)
                .ToListAsync();
        }

        public string BuildSrcSet(string? imageUrl)
        {
            var smallestImageUrl = GetSmallestImageUrl(imageUrl);
            if (string.IsNullOrEmpty(smallestImageUrl)) return string.Empty;

            if (_srcSetCache.TryGetValue(smallestImageUrl, out var cachedSrcSet))
            {
                return cachedSrcSet;
            }

            var srcSet = $"{smallestImageUrl} 320w";
            _srcSetCache[smallestImageUrl] = srcSet;
            return srcSet;
        }

        public string GetSmallestImageUrl(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return string.Empty;

            // imageUrl like /uploads/destinations/{id}/{file}
            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var rel = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.Combine(webRoot, rel);
                var dir = Path.GetDirectoryName(full) ?? webRoot;
                var name = Path.GetFileNameWithoutExtension(full);
                var baseName = name;
                var sizeSuffixes = new[] { "-320", "-640", "-1024" };
                foreach (var suffix in sizeSuffixes)
                {
                    if (baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        baseName = baseName[..^suffix.Length];
                        break;
                    }
                }

                var candidates = new[]
                {
                    Path.Combine(dir, baseName + "-320.webp"),
                    Path.Combine(dir, baseName + "-640.webp"),
                    Path.Combine(dir, baseName + "-1024.webp"),
                    Path.Combine(dir, baseName + ".webp")
                };

                foreach (var candidate in candidates)
                {
                    if (System.IO.File.Exists(candidate))
                    {
                        return "/" + Path.GetRelativePath(webRoot, candidate).Replace('\\', '/');
                    }
                }

                if (System.IO.File.Exists(full))
                {
                    return "/" + Path.GetRelativePath(webRoot, full).Replace('\\', '/');
                }

                return imageUrl;
            }
            catch
            {
                return imageUrl;
            }
        }
    }
}
