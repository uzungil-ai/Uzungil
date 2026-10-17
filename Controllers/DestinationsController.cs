using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class DestinationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DestinationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Destinations
        public async Task<IActionResult> Index()
        {
            // include related Images, Attractions, Hotels and Meals to display counts in the view
            var list = await _context.Destinations!
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .Include(d => d.Hotels)
                .Include(d => d.Meals)
                .ToListAsync();
            return View(list);
        }

        // GET: Destinations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            // eager load related images, attractions, hotels and meals for details page
            var destination = await _context.Destinations!
                .Include(d => d.Images)
                .Include(d => d.Attractions)
                .Include(d => d.Hotels)
                .Include(d => d.Meals)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (destination == null) return NotFound();

            return View(destination);
        }

        // GET: Destinations/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Destinations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Country,Description,InstagramUrl,FacebookUrl,Latitude,Longitude,EntranceFee")] Destination destination)
        {
            if (ModelState.IsValid)
            {
                _context.Add(destination);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(destination);
        }

        // GET: Destinations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var destination = await _context.Destinations!.FindAsync(id.Value);
            if (destination == null) return NotFound();
            return View(destination);
        }

        // POST: Destinations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Country,Description,InstagramUrl,FacebookUrl,Latitude,Longitude,EntranceFee")] Destination destination)
        {
            if (id != destination.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(destination);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DestinationExists(destination.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(destination);
        }

        // GET: Destinations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var destination = await _context.Destinations!
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (destination == null) return NotFound();

            return View(destination);
        }

        // POST: Destinations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var destination = await _context.Destinations!.FindAsync(id);
            if (destination != null)
            {
                _context.Destinations!.Remove(destination);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool DestinationExists(int id)
        {
            return _context.Destinations!.Any(e => e.Id == id);
        }
    }
}
