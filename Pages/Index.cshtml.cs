using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public IndexModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<Destination> Featured { get; set; } = new List<Destination>();
        public List<Destination> Latest { get; set; } = new List<Destination>();

        public async Task OnGetAsync()
        {
            Featured = await _db.Destinations!
                .Include(d => d.Images)
                .OrderBy(d => d.Name)
                .Take(100)
                .ToListAsync();

            Latest = await _db.Destinations!
                .Include(d => d.Images)
                .OrderByDescending(d => d.Id)
                .Take(100)
                .ToListAsync();
        }

        public string BuildSrcSet(string? imageUrl)
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
                var exts = new[] { ".webp", ".jpg", ".jpeg", ".png" };

                var sizes = new[] { 320, 640, 1024 };
                var parts = new List<string>();
                foreach (var s in sizes)
                {
                    var candidate = Path.Combine(dir, name + $"-{s}.webp");
                    if (System.IO.File.Exists(candidate))
                    {
                        var relPath = "/" + Path.GetRelativePath(webRoot, candidate).Replace('\\','/');
                        parts.Add($"{relPath} {s}w");
                    }
                }

                // fallback to original
                if (parts.Count == 0 && System.IO.File.Exists(full))
                {
                    var relPath = "/" + Path.GetRelativePath(webRoot, full).Replace('\\','/');
                    parts.Add($"{relPath} 640w");
                }

                return string.Join(", ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
