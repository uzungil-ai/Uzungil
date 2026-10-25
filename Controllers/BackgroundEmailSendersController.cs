using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BackgroundEmailSendersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BackgroundEmailSendersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: BackgroundEmailSenders
        public async Task<IActionResult> Index()
        {
            return View(await _context.FailedEmails!.OrderByDescending(f => f.CreatedAt).ToListAsync());
        }

        // GET: BackgroundEmailSenders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var failedEmail = await _context.FailedEmails!
                .FirstOrDefaultAsync(m => m.Id == id);
            if (failedEmail == null)
            {
                return NotFound();
            }

            return View(failedEmail);
        }

        // GET: BackgroundEmailSenders/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: BackgroundEmailSenders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,To,Subject,Body,Attempts,LastError,CreatedAt")] FailedEmail failedEmail)
        {
            if (ModelState.IsValid)
            {
                _context.Add(failedEmail);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(failedEmail);
        }

        // GET: BackgroundEmailSenders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var failedEmail = await _context.FailedEmails!.FindAsync(id);
            if (failedEmail == null)
            {
                return NotFound();
            }
            return View(failedEmail);
        }

        // POST: BackgroundEmailSenders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,To,Subject,Body,Attempts,LastError,CreatedAt")] FailedEmail failedEmail)
        {
            if (id != failedEmail.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(failedEmail);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FailedEmailExists(failedEmail.Id))
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
            return View(failedEmail);
        }

        // GET: BackgroundEmailSenders/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var failedEmail = await _context.FailedEmails!
                .FirstOrDefaultAsync(m => m.Id == id);
            if (failedEmail == null)
            {
                return NotFound();
            }

            return View(failedEmail);
        }

        // POST: BackgroundEmailSenders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var failedEmail = await _context.FailedEmails!.FindAsync(id);
            if (failedEmail != null)
            {
                _context.FailedEmails.Remove(failedEmail);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FailedEmailExists(int id)
        {
            return _context.FailedEmails!.Any(e => e.Id == id);
        }
    }
}
