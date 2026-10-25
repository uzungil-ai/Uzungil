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

        public IndexModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public List<Destination> Featured { get; set; } = new List<Destination>();
        public List<Destination> Latest { get; set; } = new List<Destination>();

        [BindProperty(SupportsGet = true)]
        public string? Name { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Country { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Conutry { get; set; }

        public bool HasActiveSearch =>
            !string.IsNullOrWhiteSpace(Name) ||
            !string.IsNullOrWhiteSpace(Country);

        public async Task OnGetAsync()
        {
            Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim();
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
            Country = string.IsNullOrWhiteSpace(Country) ? null : Country.Trim();
            Conutry = string.IsNullOrWhiteSpace(Conutry) ? null : Conutry.Trim();

            if (Name is null && Search is not null)
            {
                Name = Search;
            }

            if (Country is null && Conutry is not null)
            {
                Country = Conutry;
            }

            var baseQuery = _db.Destinations!
                .Include(d => d.Images)
                .AsQueryable();

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

            Featured = await baseQuery
                .OrderBy(d => d.Name)
                .Take(100)
                .ToListAsync();

            Latest = await baseQuery
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
