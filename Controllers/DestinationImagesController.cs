using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class DestinationImagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DestinationImagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DestinationImages
        public async Task<IActionResult> Index()
        {
            var images = await _context.DestinationImages!
                .Include(di => di.Destination)
                .OrderBy(di => di.DestinationId)
                .ToListAsync();
            return View(images);
        }

        // GET: DestinationImages/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var image = await _context.DestinationImages!
                .Include(di => di.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (image == null) return NotFound();

            return View(image);
        }

        // GET: DestinationImages/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationId);
            ViewData["PreselectedDestinationId"] = destinationId;
            return View();
        }

        // POST: DestinationImages/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Url,DestinationId,Order,AltText")] DestinationImage destinationImage)
        {
            if (ModelState.IsValid)
            {
                _context.Add(destinationImage);
                await _context.SaveChangesAsync();
                if (destinationImage.DestinationId > 0)
                {
                    return RedirectToAction("Details", "Destinations", new { id = destinationImage.DestinationId });
                }
                return RedirectToAction(nameof(Index));
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? (e.Exception?.Message ?? "") : e.ErrorMessage)
                .Where(s => !string.IsNullOrEmpty(s));
            if (errors.Any()) TempData["ModelErrors"] = string.Join("; ", errors);
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationImage.DestinationId);
            return View(destinationImage);
        }

        // GET: DestinationImages/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var destinationImage = await _context.DestinationImages!.FindAsync(id.Value);
            if (destinationImage == null) return NotFound();
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationImage.DestinationId);
            return View(destinationImage);
        }

        // POST: DestinationImages/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Url,DestinationId,Order,AltText")] DestinationImage destinationImage)
        {
            if (id != destinationImage.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(destinationImage);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DestinationImageExists(destinationImage.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationImage.DestinationId);
            return View(destinationImage);
        }

        // GET: DestinationImages/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var destinationImage = await _context.DestinationImages!
                .Include(di => di.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (destinationImage == null) return NotFound();

            return View(destinationImage);
        }

        // POST: DestinationImages/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var destinationImage = await _context.DestinationImages!.FindAsync(id);
            if (destinationImage != null)
            {
                _context.DestinationImages!.Remove(destinationImage);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool DestinationImageExists(int id)
        {
            return _context.DestinationImages!.Any(e => e.Id == id);
        }

        // Debug endpoint: /DestinationImages/Debug
        public async Task<IActionResult> Debug()
        {
            var total = await _context.DestinationImages!.CountAsync();
            return Content($"DestinationImages count: {total}");
        }
    }
}
