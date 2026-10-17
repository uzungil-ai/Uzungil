using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class MealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Meals
        public async Task<IActionResult> Index()
        {
            var meals = await _context.Meals!
                .Include(m => m.Destination)
                .ToListAsync();
            return View(meals);
        }

        // GET: Meals/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var meal = await _context.Meals!
                .Include(m => m.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (meal == null) return NotFound();

            return View(meal);
        }

        // GET: Meals/Create
        public async Task<IActionResult> Create(int? destinationId)
        {
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", destinationId);
            return View();
        }

        // POST: Meals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Address,Phone,Price,DestinationId")] Meal meal)
        {
            if (ModelState.IsValid)
            {
                _context.Add(meal);
                await _context.SaveChangesAsync();
                if (meal.DestinationId > 0)
                {
                    return RedirectToAction("Details", "Destinations", new { id = meal.DestinationId });
                }
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", meal.DestinationId);
            return View(meal);
        }

        // GET: Meals/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var meal = await _context.Meals!.FindAsync(id.Value);
            if (meal == null) return NotFound();
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", meal.DestinationId);
            return View(meal);
        }

        // POST: Meals/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Address,Phone,Price,DestinationId")] Meal meal)
        {
            if (id != meal.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(meal);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MealExists(meal.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            var destinations = await (_context.Destinations?.OrderBy(d => d.Name).ToListAsync() ?? Task.FromResult(new System.Collections.Generic.List<Roma.Models.Destination>()));
            ViewData["DestinationId"] = new SelectList(destinations, "Id", "Name", meal.DestinationId);
            return View(meal);
        }

        // GET: Meals/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var meal = await _context.Meals!
                .Include(m => m.Destination)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (meal == null) return NotFound();

            return View(meal);
        }

        // POST: Meals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var meal = await _context.Meals!.FindAsync(id);
            if (meal != null)
            {
                _context.Meals!.Remove(meal);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool MealExists(int id)
        {
            return _context.Meals!.Any(e => e.Id == id);
        }
    }
}
