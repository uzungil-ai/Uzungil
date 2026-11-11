using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    [Authorize]
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubscribersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Subscribers
        public async Task<IActionResult> Index()
        {
            var pending = await _context.Subscribers!
                .AsNoTracking()
                .Where(x => x.ConsentGiven && !x.IsConfirmed)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var confirmed = await _context.Subscribers!
                .AsNoTracking()
                .Where(x => x.ConsentGiven && x.IsConfirmed)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var cancelled = await _context.Subscribers!
                .AsNoTracking()
                .Where(x => !x.ConsentGiven)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var sentMessages = await _context.SentEmails!
                .AsNoTracking()
                .Where(x => x.Subject.Contains("Abonelik") || x.Subject.Contains("destinasyon") || x.Subject.Contains("Destinasyon"))
                .OrderByDescending(x => x.SentAt)
                .Take(200)
                .ToListAsync();

            var pendingReviews = await _context.Reviews!
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Attraction)
                .Where(r => !r.IsApproved && !r.IsRejected)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            var approvedReviews = await _context.Reviews!
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Attraction)
                .Where(r => r.IsApproved && !r.IsRejected)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            var rejectedReviews = await _context.Reviews!
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Attraction)
                .Where(r => r.IsRejected)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();

            var vm = new SubscriberDashboardViewModel
            {
                PendingSubscribers = pending,
                ConfirmedSubscribers = confirmed,
                CancelledSubscribers = cancelled,
                SentMessages = sentMessages,
                PendingReviews = pendingReviews,
                ApprovedReviews = approvedReviews,
                RejectedReviews = rejectedReviews
            };

            return View(vm);
        }

        // GET: Subscribers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subscriber = await _context.Subscribers!
                .FirstOrDefaultAsync(m => m.Id == id);
            if (subscriber == null)
            {
                return NotFound();
            }

            return View(subscriber);
        }

        // GET: Subscribers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Subscribers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Email,ConsentGiven,IsConfirmed,CreatedAt,ConfirmationToken,ConfirmationExpiresAt,UnsubscribeToken")] Subscriber subscriber)
        {
            if (ModelState.IsValid)
            {
                _context.Add(subscriber);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(subscriber);
        }

        // GET: Subscribers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subscriber = await _context.Subscribers!.FindAsync(id);
            if (subscriber == null)
            {
                return NotFound();
            }
            return View(subscriber);
        }

        // POST: Subscribers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Email,ConsentGiven,IsConfirmed,CreatedAt,ConfirmationToken,ConfirmationExpiresAt,UnsubscribeToken")] Subscriber subscriber)
        {
            if (id != subscriber.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(subscriber);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SubscriberExists(subscriber.Id))
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
            return View(subscriber);
        }

        // GET: Subscribers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subscriber = await _context.Subscribers!
                .FirstOrDefaultAsync(m => m.Id == id);
            if (subscriber == null)
            {
                return NotFound();
            }

            return View(subscriber);
        }

        // POST: Subscribers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subscriber = await _context.Subscribers!.FindAsync(id);
            if (subscriber != null)
            {
                _context.Subscribers.Remove(subscriber);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id, string? returnUrl = null)
        {
            var subscriber = await _context.Subscribers!
                .FirstOrDefaultAsync(x => x.Id == id);

            if (subscriber == null)
            {
                return NotFound();
            }

            subscriber.ConsentGiven = true;
            subscriber.IsConfirmed = true;
            subscriber.ConfirmationToken = null;
            subscriber.ConfirmationExpiresAt = null;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Abonelik onaylandı.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id, string? returnUrl = null)
        {
            var subscriber = await _context.Subscribers!
                .FirstOrDefaultAsync(x => x.Id == id);

            if (subscriber == null)
            {
                return NotFound();
            }

            subscriber.ConsentGiven = false;
            subscriber.IsConfirmed = false;
            subscriber.ConfirmationToken = null;
            subscriber.ConfirmationExpiresAt = null;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Abonelik reddedildi.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool SubscriberExists(int id)
        {
            return _context.Subscribers!.Any(e => e.Id == id);
        }
    }
}