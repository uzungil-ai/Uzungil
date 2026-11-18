using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Roma.Data;
using Roma.Models;
using System.Text.RegularExpressions;

namespace Roma.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly Dictionary<string, string> _srcSetCache = new(StringComparer.OrdinalIgnoreCase);
        private const string SplashImageCacheKey = "IndexModel.SplashImages";
        private static readonly string[] ManualCategoryOptions =
        {
            "Anıtlar",
            "Cami",
            "Destinasyon",
            "Doğa",
            "Hanlar",
            "Havra",
            "Kervansaray",
            "Kilise",
            "Külliye",
            "Mozole",
            "Müzeler",
            "Parklar",
            "Piknik Alanı",
            "Saraylar",
            "Sinegog",
            "Tapınak",
            "Yemekler",
            "Oteller",
            "Misafirhane",
            "Darulşifahane",
            "Yöresel Yemekler",
            "Otoparklar",
            "Piknik Alanları",
            "Üniversiteler",
            "Tiyatrolar",
            "Operalar",
            "Kayak Tesisleri",
            "Kaleler",
            "Fast Food",
            "Manastırlar",
            "Kiralık Evler"
        };

        public IndexModel(ApplicationDbContext db, IWebHostEnvironment env, IMemoryCache cache)
        {
            _db = db;
            _env = env;
            _cache = cache;
        }

        public List<Destination> Featured { get; set; } = new List<Destination>();
        public List<Destination> Latest { get; set; } = new List<Destination>();
        public List<string> CategoryOptions { get; set; } = new List<string>();
        public int TotalDestinationCount { get; set; }
        public int TotalPhotoCount { get; set; }
        public string RandomSplashImage { get; private set; } = "/images/italya.jpg";

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

        [BindProperty(SupportsGet = true)]
        public string? Subscriber { get; set; }

        public bool IsSubscriberMode { get; private set; }

        public bool HasActiveSearch =>
            !string.IsNullOrWhiteSpace(Search) ||
            !string.IsNullOrWhiteSpace(Name) ||
            !string.IsNullOrWhiteSpace(Country) ||
            !string.IsNullOrWhiteSpace(Category);

        public async Task OnGetAsync()
        {
            TotalDestinationCount = await _db.Destinations!.CountAsync();
            TotalPhotoCount = await _db.DestinationImages!.CountAsync();

            var dbCategoryOptions = await _db.Attractions!
                .AsNoTracking()
                .Where(a => a.Category != null && a.Category.Trim() != string.Empty)
                .Select(a => a.Category.Trim())
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            CategoryOptions = ManualCategoryOptions
                .Concat(dbCategoryOptions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
            Conutry = string.IsNullOrWhiteSpace(Conutry) ? null : Conutry.Trim();
            Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim();
            Subscriber = string.IsNullOrWhiteSpace(Subscriber) ? null : Subscriber.Trim();

            IsSubscriberMode = string.Equals(Subscriber, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Subscriber, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Subscriber, "yes", StringComparison.OrdinalIgnoreCase);

            if (Country is null && Conutry is not null)
            {
                Country = Conutry;
            }

            var baseQuery = _db.Destinations!
                .AsNoTracking()
                .AsQueryable();

            if (Search is not null)
            {
                var pattern = $"%{Search}%";
                var nameMatchedIds = await _db.Destinations!
                    .AsNoTracking()
                    .Where(d => d.Name != null && EF.Functions.Like(d.Name, pattern))
                    .Select(d => d.Id)
                    .ToListAsync();

                if (nameMatchedIds.Count > 0)
                {
                    baseQuery = baseQuery.Where(d => nameMatchedIds.Contains(d.Id));
                }
                else
                {
                    var descriptionPattern = $"%{Search}%";
                    var descriptionMatchedIds = await _db.Destinations!
                        .AsNoTracking()
                        .Where(d => d.Description != null && EF.Functions.Like(d.Description, descriptionPattern))
                        .Select(d => d.Id)
                        .ToListAsync();

                    if (descriptionMatchedIds.Count > 0)
                    {
                        baseQuery = baseQuery.Where(d => descriptionMatchedIds.Contains(d.Id));
                        goto SkipDescriptionPrefixFallback;
                    }

                    var prefixLength = Math.Min(3, Search.Length);
                    var prefix = Search[..prefixLength];

                    var descriptionPrefixMatchedIds = (await _db.Destinations!
                        .AsNoTracking()
                        .Where(d => d.Description != null)
                        .Select(d => new { d.Id, d.Description })
                        .ToListAsync())
                        .Where(d => ContainsWordStartingWith(d.Description, prefix))
                        .Select(d => d.Id)
                        .ToList();

                    baseQuery = baseQuery.Where(d => descriptionPrefixMatchedIds.Contains(d.Id));

                SkipDescriptionPrefixFallback:;
                }
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

            var latestIds = await baseQuery
                .OrderByDescending(d => d.Id)
                .Select(d => d.Id)
                .Take(24)
                .ToListAsync();

            var featuredCandidateIds = await baseQuery
                .OrderByDescending(d => d.Id)
                .Select(d => d.Id)
                .Take(300)
                .ToListAsync();

            var featuredIds = TakeRandom(featuredCandidateIds, 10);

            var allNeededIds = latestIds
                .Concat(featuredIds)
                .Distinct()
                .ToList();

            var hydratedDestinations = await _db.Destinations!
                .AsNoTracking()
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .Where(d => allNeededIds.Contains(d.Id))
                .ToListAsync();

            var destinationById = hydratedDestinations.ToDictionary(d => d.Id);

            Latest = latestIds
                .Where(id => destinationById.ContainsKey(id))
                .Select(id => destinationById[id])
                .ToList();

            Featured = featuredIds
                .Where(id => destinationById.ContainsKey(id))
                .Select(id => destinationById[id])
                .ToList();

            var fallbackSplashImage = GetSmallestImageUrl(Featured.FirstOrDefault()?.Images.FirstOrDefault()?.Url);
            if (string.IsNullOrWhiteSpace(fallbackSplashImage))
            {
                fallbackSplashImage = "/images/italya.jpg";
            }

            var splashImages = GetSplashImagesFromAppFiles();
            RandomSplashImage = splashImages.Count > 0
                ? splashImages[Random.Shared.Next(splashImages.Count)]
                : fallbackSplashImage;
        }

        private IReadOnlyList<string> GetSplashImagesFromAppFiles()
        {
            return _cache.GetOrCreate(SplashImageCacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                var splashFolder = Path.Combine(_env.WebRootPath, "images", "splash");
                if (!Directory.Exists(splashFolder))
                {
                    return Array.Empty<string>();
                }

                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png", ".webp"
                };

                return Directory
                    .EnumerateFiles(splashFolder, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
                    .Select(file => $"/images/splash/{Path.GetFileName(file)}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }) ?? Array.Empty<string>();
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

        private static bool ContainsWordStartingWith(string? text, string prefix)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            text = Regex.Replace(text, "<[^>]*>", " ");

            var separators = new[]
            {
                ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?',
                '(', ')', '[', ']', '{', '}', '"', '\'', '-', '/', '\\', '|', '_', '<', '>', '=', '&'
            };

            var words = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            return words.Any(w => w.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
        }

        private static List<int> TakeRandom(List<int> source, int count)
        {
            if (source.Count <= count)
            {
                return source;
            }

            var copy = source.ToList();
            for (var i = copy.Count - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }

            return copy.Take(count).ToList();
        }
    }
}
