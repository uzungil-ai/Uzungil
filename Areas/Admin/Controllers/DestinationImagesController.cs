using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Roma.Data;
using Roma.Models;

namespace Roma.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DestinationImagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DestinationImagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/DestinationImages
        public async Task<IActionResult> Index()
        {
            var images = await _context.DestinationImages!
                .Include(di => di.Destination)
                .OrderBy(di => di.DestinationId)
                .ToListAsync();
            return View(images);
        }

        // GET: Admin/DestinationImages/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var image = await _context.DestinationImages!
                .Include(di => di.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (image == null) return NotFound();

            return View(image);
        }

        // GET: Admin/DestinationImages/Create
        public async Task<IActionResult> Create()
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name");
            return View();
        }

        // POST: Admin/DestinationImages/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Url,DestinationId,Order,AltText")] DestinationImage destinationImage)
        {
            if (ModelState.IsValid)
            {
                _context.Add(destinationImage);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationImage.DestinationId);
            return View(destinationImage);
        }

        // GET: Admin/DestinationImages/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var destinationImage = await _context.DestinationImages!.FindAsync(id.Value);
            if (destinationImage == null) return NotFound();
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationImage.DestinationId);
            return View(destinationImage);
        }

        // POST: Admin/DestinationImages/Edit/5
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

        // GET: Admin/DestinationImages/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var destinationImage = await _context.DestinationImages!
                .Include(di => di.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (destinationImage == null) return NotFound();

            return View(destinationImage);
        }

        // POST: Admin/DestinationImages/Delete/5
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
    }
}
