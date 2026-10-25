using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class AttractionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AttractionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Attractions
        public async Task<IActionResult> Index()
        {
            var attractions = await _context.Attractions!
                .Include(a => a.Destination)
                .ToListAsync();
            return View(attractions);
        }

        // GET: Attractions/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var attraction = await _context.Attractions!
                .AsNoTracking()
                .AsSplitQuery()
                .Include(a => a.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (attraction == null) return NotFound();

            return View(attraction);
        }

        // GET: Attractions/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            await LoadDestinationSelectListAsync(destinationId);
            ViewBag.ReturnToDestination = destinationId.HasValue;
            return View();
        }

        // POST: Attractions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Category,Address,AverageRating,DestinationId")] Attraction attraction, bool returnToDestination = false)
        {
            ModelState.Remove(nameof(Attraction.Destination));

            if (attraction.AverageRating < 1 || attraction.AverageRating > 5)
            {
                ModelState.AddModelError(nameof(Attraction.AverageRating), "Puan 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(attraction);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cazibe noktası başarıyla kaydedildi.";

                if (returnToDestination)
                {
                    return RedirectToAction("Details", "Destinations", new { id = attraction.DestinationId });
                }

                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "Kayıt yapılamadı. Lütfen zorunlu alanları kontrol edin.";
            await LoadDestinationSelectListAsync(attraction.DestinationId);
            ViewBag.ReturnToDestination = returnToDestination;
            return View(attraction);
        }

        // GET: Attractions/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var attraction = await _context.Attractions!
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id.Value);
            if (attraction == null) return NotFound();

            await LoadDestinationSelectListAsync(attraction.DestinationId);
            return View(attraction);
        }

        // POST: Attractions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Category,Address,AverageRating,DestinationId")] Attraction attraction)
        {
            if (id != attraction.Id) return NotFound();

            ModelState.Remove(nameof(Attraction.Destination));

            if (attraction.AverageRating < 1 || attraction.AverageRating > 5)
            {
                ModelState.AddModelError(nameof(Attraction.AverageRating), "Puan 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(attraction);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AttractionExists(attraction.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await LoadDestinationSelectListAsync(attraction.DestinationId);
            return View(attraction);
        }

        // GET: Attractions/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var attraction = await _context.Attractions!
                .Include(a => a.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (attraction == null) return NotFound();

            return View(attraction);
        }

        // POST: Attractions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var attraction = await _context.Attractions!.FindAsync(id);
            if (attraction != null)
            {
                _context.Attractions!.Remove(attraction);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool AttractionExists(int id)
        {
            return _context.Attractions!.Any(e => e.Id == id);
        }

        private async Task LoadDestinationSelectListAsync(int? selectedDestinationId)
        {
            var destinationItems = _context.Destinations == null
                ? new List<SelectListItem>()
                : await _context.Destinations
                    .AsNoTracking()
                    .OrderBy(d => d.Name)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Name
                    })
                    .ToListAsync();

            ViewData["DestinationId"] = new SelectList(destinationItems, "Value", "Text", selectedDestinationId?.ToString());
        }
    }
}
