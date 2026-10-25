using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class FlavorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FlavorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Flavors
        public async Task<IActionResult> Index()
        {
            var flavors = await _context.Flavors!
                .Include(f => f.Destination)
                .ToListAsync();
            return View(flavors);
        }

        // GET: Flavors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var flavor = await _context.Flavors!
                .Include(f => f.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (flavor == null) return NotFound();

            return View(flavor);
        }

        // GET: Flavors/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationId);
            ViewData["PreselectedDestinationId"] = destinationId;
            return View();
        }

        // POST: Flavors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,DestinationId")] Flavor flavor)
        {
            ModelState.Remove("Destination");

            if (ModelState.IsValid)
            {
                _context.Add(flavor);
                await _context.SaveChangesAsync();
                if (flavor.DestinationId > 0)
                {
                    return RedirectToAction("Details", "Destinations", new { id = flavor.DestinationId });
                }
                return RedirectToAction(nameof(Index));
            }

            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", flavor.DestinationId);
            return View(flavor);
        }

        // GET: Flavors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var flavor = await _context.Flavors!.FindAsync(id.Value);
            if (flavor == null) return NotFound();

            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", flavor.DestinationId);
            return View(flavor);
        }

        // POST: Flavors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,DestinationId")] Flavor flavor)
        {
            if (id != flavor.Id) return NotFound();

            ModelState.Remove("Destination");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(flavor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FlavorExists(flavor.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", flavor.DestinationId);
            return View(flavor);
        }

        // GET: Flavors/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var flavor = await _context.Flavors!
                .Include(f => f.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (flavor == null) return NotFound();

            return View(flavor);
        }

        // POST: Flavors/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var flavor = await _context.Flavors!.FindAsync(id);
            if (flavor != null)
            {
                _context.Flavors!.Remove(flavor);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool FlavorExists(int id)
        {
            return _context.Flavors!.Any(e => e.Id == id);
        }
    }
}
