using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class ParkingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ParkingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Parkings
        public async Task<IActionResult> Index()
        {
            var parkings = await _context.Parkings!
                .Include(p => p.Destination)
                .ToListAsync();
            return View(parkings);
        }

        // GET: Parkings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var parking = await _context.Parkings!
                .Include(p => p.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (parking == null) return NotFound();

            return View(parking);
        }

        // GET: Parkings/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationId);
            ViewData["PreselectedDestinationId"] = destinationId;
            return View();
        }

        // POST: Parkings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Capacity,IsPaid,Fee,HourlyRate,DestinationId")] Parking parking)
        {
            ModelState.Remove("Destination");

            if (ModelState.IsValid)
            {
                _context.Add(parking);
                await _context.SaveChangesAsync();
                if (parking.DestinationId > 0)
                {
                    return RedirectToAction("Details", "Destinations", new { id = parking.DestinationId });
                }
                return RedirectToAction(nameof(Index));
            }

            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", parking.DestinationId);
            return View(parking);
        }

        // GET: Parkings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var parking = await _context.Parkings!.FindAsync(id.Value);
            if (parking == null) return NotFound();
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", parking.DestinationId);
            return View(parking);
        }

        // POST: Parkings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Capacity,IsPaid,Fee,HourlyRate,DestinationId")] Parking parking)
        {
            if (id != parking.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(parking);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ParkingExists(parking.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", parking.DestinationId);
            return View(parking);
        }

        // GET: Parkings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var parking = await _context.Parkings!
                .Include(p => p.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (parking == null) return NotFound();

            return View(parking);
        }

        // POST: Parkings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var parking = await _context.Parkings!.FindAsync(id);
            if (parking != null)
            {
                _context.Parkings!.Remove(parking);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ParkingExists(int id)
        {
            return _context.Parkings!.Any(e => e.Id == id);
        }
    }
}