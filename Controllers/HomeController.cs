using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using System.Diagnostics;

namespace Roma.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? country, string? category, string? search)
        {
            country = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
            category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

            var destinationsQuery = _context.Destinations!
                .AsNoTracking()
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .AsQueryable();

            if (search is not null)
            {
                var normalizedSearch = search.Trim().ToLower();
                var searchPrefix = normalizedSearch.Length >= 3
                    ? normalizedSearch[..3]
                    : normalizedSearch;

                destinationsQuery = destinationsQuery.Where(d =>
                    d.Name != null && d.Name.ToLower().StartsWith(searchPrefix));
            }

            if (country is not null)
            {
                var countryPattern = $"%{country}%";
                destinationsQuery = destinationsQuery.Where(d => d.Country != null && EF.Functions.Like(d.Country, countryPattern));
            }

            if (category is not null)
            {
                var categoryPattern = $"%{category}%";
                destinationsQuery = destinationsQuery.Where(d => d.Attractions.Any(a => a.Category != null && EF.Functions.Like(a.Category, categoryPattern)));
            }

            var destinations = await destinationsQuery
                .OrderBy(d => d.Name)
                .ToListAsync();

            var categories = await _context.Attractions!
                .AsNoTracking()
                .Select(a => a.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.CountryFilter = country;
            ViewBag.CategoryFilter = category;
            ViewBag.SearchFilter = search;
            ViewBag.CategoryOptions = categories;

            return View(destinations);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
