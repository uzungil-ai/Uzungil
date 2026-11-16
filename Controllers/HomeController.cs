using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using System.Diagnostics;
using System.Globalization;

namespace Roma.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? name, string? search, string? country, string? conutry, string? category)
        {
            name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            country = string.IsNullOrWhiteSpace(country) ? null : country.Trim();
            conutry = string.IsNullOrWhiteSpace(conutry) ? null : conutry.Trim();
            category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

            // Geriye dönük destek: eski "search" parametresi name olarak kabul edilir.
            if (name is null && search is not null)
            {
                name = search;
            }

            // Eski/yanlış yazım "conutry" ile gelen istekleri de destekle.
            if (country is null && conutry is not null)
            {
                country = conutry;
            }

            var destinationsQuery = _context.Destinations!
                .AsNoTracking()
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .AsQueryable();

            // Not: name/country aramasını veritabanı kolasyon farklılıklarından etkilenmemesi için
            // listeden sonra uyguluyoruz.

            if (category is not null)
            {
                var categoryPattern = $"%{category}%";
                destinationsQuery = destinationsQuery.Where(d => d.Attractions.Any(a => a.Category != null && EF.Functions.Like(a.Category, categoryPattern)));
            }

            var destinations = await destinationsQuery
                .OrderBy(d => d.Name)
                .ToListAsync();

            if (name is not null)
            {
                destinations = destinations
                    .Where(d =>
                        ContainsSearch(d.Name, name) ||
                        ContainsSearch(d.Country, name) ||
                        ContainsSearch(d.Description, name))
                    .ToList();
            }

            if (country is not null)
            {
                destinations = destinations
                    .Where(d =>
                        ContainsSearch(d.Name, country) ||
                        ContainsSearch(d.Country, country) ||
                        ContainsSearch(d.Description, country))
                    .ToList();
            }

            var categories = await _context.Attractions!
                .AsNoTracking()
                .Select(a => a.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.CountryFilter = country;
            ViewBag.CategoryFilter = category;
            ViewBag.NameFilter = name;
            ViewBag.CategoryOptions = categories;

            return View(destinations);
        }

        public async Task<IActionResult> Hakkinda()
        {
            ViewBag.TotalDestinationCount = await _context.Destinations!.CountAsync();
            ViewBag.TotalPhotoCount = await _context.DestinationImages!.CountAsync();
            return View("Hakkında");
        }

        public async Task<IActionResult> Hakkında()
        {
            ViewBag.TotalDestinationCount = await _context.Destinations!.CountAsync();
            ViewBag.TotalPhotoCount = await _context.DestinationImages!.CountAsync();
            return View();
        }

        public IActionResult Ekibimiz()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error is not null)
            {
                _logger.LogError(
                    exceptionFeature.Error,
                    "Unhandled exception captured on path: {Path}",
                    exceptionFeature.Path);
            }

            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static bool ContainsSearch(string? source, string keyword)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                source,
                keyword,
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
        }
    }
}
