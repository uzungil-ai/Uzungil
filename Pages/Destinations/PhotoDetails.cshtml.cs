using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Destinations
{
    [AllowAnonymous]
    public class PhotoDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly Dictionary<string, string> _srcSetCache = new(StringComparer.OrdinalIgnoreCase);

        public PhotoDetailsModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public Destination? Destination { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Destination = await _db.Destinations!
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                    .ThenInclude(a => a.Reviews)
                        .ThenInclude(r => r.User)
                .Include(d => d.Hotels)
                .Include(d => d.Meals)
                .Include(d => d.Flavors)
                .Include(d => d.Parkings)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (Destination == null)
            {
                return NotFound();
            }

            return Page();
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
