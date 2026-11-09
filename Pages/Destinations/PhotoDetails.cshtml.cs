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
            if (string.IsNullOrEmpty(imageUrl)) return string.Empty;

            if (_srcSetCache.TryGetValue(imageUrl, out var cachedSrcSet))
            {
                return cachedSrcSet;
            }

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var rel = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var full = Path.Combine(webRoot, rel);
                var dir = Path.GetDirectoryName(full) ?? webRoot;
                var name = Path.GetFileNameWithoutExtension(full);

                var sizes = new[] { 320, 640, 1024 };
                var parts = new List<string>();

                foreach (var s in sizes)
                {
                    var candidate = Path.Combine(dir, name + $"-{s}.webp");
                    if (System.IO.File.Exists(candidate))
                    {
                        var relPath = "/" + Path.GetRelativePath(webRoot, candidate).Replace('\\', '/');
                        parts.Add($"{relPath} {s}w");
                    }
                }

                if (parts.Count == 0 && System.IO.File.Exists(full))
                {
                    var relPath = "/" + Path.GetRelativePath(webRoot, full).Replace('\\', '/');
                    parts.Add($"{relPath} 640w");
                }

                var srcSet = string.Join(", ", parts);
                _srcSetCache[imageUrl] = srcSet;
                return srcSet;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
