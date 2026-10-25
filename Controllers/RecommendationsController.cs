using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class RecommendationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecommendationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Recommendations
        public async Task<IActionResult> Index()
        {
            var recommendations = await _context.Recommendations!
                .Include(r => r.Destination)
                .ToListAsync();
            return View(recommendations);
        }

        // GET: Recommendations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var recommendation = await _context.Recommendations!
                .Include(r => r.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (recommendation == null) return NotFound();

            return View(recommendation);
        }

        // GET: Recommendations/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationId);
            ViewData["PreselectedDestinationId"] = destinationId;
            return View();
        }

        // POST: Recommendations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,Url,UserId,DestinationId,IsOfficial")] Recommendation recommendation)
        {
            if (ModelState.IsValid)
            {
                recommendation.CreatedAt = DateTime.UtcNow;
                _context.Add(recommendation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", recommendation.DestinationId);
            ViewData["PreselectedDestinationId"] = recommendation.DestinationId;
            return View(recommendation);
        }

        // GET: Recommendations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var recommendation = await _context.Recommendations!.FindAsync(id.Value);
            if (recommendation == null) return NotFound();

            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", recommendation.DestinationId);
            return View(recommendation);
        }

        // POST: Recommendations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Url,UserId,DestinationId,IsOfficial")] Recommendation input)
        {
            if (id != input.Id) return NotFound();

            var recommendation = await _context.Recommendations!.FirstOrDefaultAsync(r => r.Id == id);
            if (recommendation == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    recommendation.Title = input.Title;
                    recommendation.Description = input.Description;
                    recommendation.Url = input.Url;
                    recommendation.UserId = input.UserId;
                    recommendation.DestinationId = input.DestinationId;
                    recommendation.IsOfficial = input.IsOfficial;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RecommendationExists(input.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", input.DestinationId);
            return View(input);
        }

        // GET: Recommendations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var recommendation = await _context.Recommendations!
                .Include(r => r.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (recommendation == null) return NotFound();

            return View(recommendation);
        }

        // POST: Recommendations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var recommendation = await _context.Recommendations!.FindAsync(id);
            if (recommendation != null)
            {
                _context.Recommendations!.Remove(recommendation);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool RecommendationExists(int id)
        {
            return _context.Recommendations!.Any(e => e.Id == id);
        }
    }
}