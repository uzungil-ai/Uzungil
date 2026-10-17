using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class HotelsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HotelsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Hotels
        public async Task<IActionResult> Index()
        {
            var hotels = await _context.Hotels!
                .Include(h => h.Destination)
                .ToListAsync();
            return View(hotels);
        }

        // GET: Hotels/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var hotel = await _context.Hotels!
                .Include(h => h.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (hotel == null) return NotFound();

            return View(hotel);
        }

        // GET: Hotels/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationId);
            ViewData["PreselectedDestinationId"] = destinationId;
            return View();
        }

        // POST: Hotels/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Address,Phone,Website,PriceFrom,PriceTo,DestinationId")] Hotel hotel)
        {
            // Navigation properties are not bound in the form; remove their ModelState entries to avoid "required" validation failures
            ModelState.Remove("Destination");

            if (ModelState.IsValid)
            {
                _context.Add(hotel);
                await _context.SaveChangesAsync();
                if (hotel.DestinationId > 0)
                {
                    return RedirectToAction("Details", "Destinations", new { id = hotel.DestinationId });
                }
                return RedirectToAction(nameof(Index));
            }
            // collect model state errors for easier debugging in the view
            var errors = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? (e.Exception?.Message ?? "") : e.ErrorMessage)
                .Where(s => !string.IsNullOrEmpty(s));
            if (errors.Any())
            {
                TempData["ModelErrors"] = string.Join("; ", errors);
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", hotel.DestinationId);
            return View(hotel);
        }

        // GET: Hotels/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var hotel = await _context.Hotels!.FindAsync(id.Value);
            if (hotel == null) return NotFound();
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", hotel.DestinationId);
            return View(hotel);
        }

        // POST: Hotels/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Address,Phone,Website,PriceFrom,PriceTo,DestinationId")] Hotel hotel)
        {
            if (id != hotel.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(hotel);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HotelExists(hotel.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", hotel.DestinationId);
            return View(hotel);
        }

        // GET: Hotels/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var hotel = await _context.Hotels!
                .Include(h => h.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (hotel == null) return NotFound();

            return View(hotel);
        }

        // POST: Hotels/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hotel = await _context.Hotels!.FindAsync(id);
            if (hotel != null)
            {
                _context.Hotels!.Remove(hotel);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool HotelExists(int id)
        {
            return _context.Hotels!.Any(e => e.Id == id);
        }

        // Debug endpoint: /Hotels/Debug
        public async Task<IActionResult> Debug()
        {
            var total = await _context.Hotels!.CountAsync();
            return Content($"Hotels count: {total}");
        }
    }
}
