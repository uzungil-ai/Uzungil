using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Services;

namespace Roma.Controllers
{
    public class DemoChatServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DemoChatServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DemoChatServices
        public async Task<IActionResult> Index()
        {
            return View(await _context.DemoChatServices!.ToListAsync());
        }

        // GET: DemoChatServices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var demoChatService = await _context.DemoChatServices!
                .FirstOrDefaultAsync(m => m.Id == id);
            if (demoChatService == null)
            {
                return NotFound();
            }

            return View(demoChatService);
        }

        // GET: DemoChatServices/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DemoChatServices/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Input,SessionId,Reply,CreatedAt")] DemoChatService demoChatService)
        {
            if (ModelState.IsValid)
            {
                _context.Add(demoChatService);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(demoChatService);
        }

        // GET: DemoChatServices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var demoChatService = await _context.DemoChatServices!.FindAsync(id);
            if (demoChatService == null)
            {
                return NotFound();
            }
            return View(demoChatService);
        }

        // POST: DemoChatServices/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Input,SessionId,Reply,CreatedAt")] DemoChatService demoChatService)
        {
            if (id != demoChatService.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(demoChatService);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DemoChatServiceExists(demoChatService.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(demoChatService);
        }

        // GET: DemoChatServices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var demoChatService = await _context.DemoChatServices!
                .FirstOrDefaultAsync(m => m.Id == id);
            if (demoChatService == null)
            {
                return NotFound();
            }

            return View(demoChatService);
        }

        // POST: DemoChatServices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var demoChatService = await _context.DemoChatServices!.FindAsync(id);
            if (demoChatService != null)
            {
                _context.DemoChatServices.Remove(demoChatService);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DemoChatServiceExists(int id)
        {
            return _context.DemoChatServices!.Any(e => e.Id == id);
        }
    }
}
