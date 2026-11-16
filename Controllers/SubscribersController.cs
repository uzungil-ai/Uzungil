using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    [Authorize(Roles = "Admin")]
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

    }
}