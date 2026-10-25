using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reviews
        public async Task<IActionResult> Index()
        {
            var reviews = await _context.Reviews!
                .AsNoTracking()
                .Include(r => r.Attraction)
                .Include(r => r.User)
                .ToListAsync();
            return View(reviews);
        }

        // GET: Reviews/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var review = await _context.Reviews!
                .Include(r => r.Attraction)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (review == null) return NotFound();

            return View(review);
        }

        // GET: Reviews/Create
        public async Task<IActionResult> Create(int? attractionId)
        {
            var attractions = await (_context.Attractions?
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .ToListAsync() ?? Task.FromResult(new List<Attraction>()));

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isUserLocked = User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(currentUserId);

            if (!isUserLocked)
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
                ViewData["UserId"] = new SelectList(users, "Id", "UserName");
            }

            ViewData["AttractionId"] = new SelectList(attractions, "Id", "Name", attractionId);
            ViewBag.UserLocked = isUserLocked;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.ReturnToAttraction = attractionId.HasValue;
            ViewBag.AttractionId = attractionId;

            return View(new Review
            {
                Comment = string.Empty,
                UserId = currentUserId ?? string.Empty,
                AttractionId = attractionId ?? 0,
                User = null!,
                Attraction = null!
            });
        }

        // POST: Reviews/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Comment,Rating,UserId,AttractionId")] Review review, bool returnToAttraction = false)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(currentUserId))
            {
                review.UserId = currentUserId;
            }

            ModelState.Remove(nameof(Review.User));
            ModelState.Remove(nameof(Review.Attraction));

            if (review.Rating < 1 || review.Rating > 5)
            {
                ModelState.AddModelError(nameof(Review.Rating), "Puan 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                review.CreatedAt = DateTime.Now;
                review.IsApproved = false;
                review.IsRejected = false;
                _context.Add(review);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Yorumunuz alındı. Yönetici onayından sonra yayınlanacaktır.";
                if (returnToAttraction)
                {
                    return RedirectToAction("Details", "Attractions", new { id = review.AttractionId });
                }
                return RedirectToAction(nameof(Index));
            }

            var attractions = await (_context.Attractions?
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .ToListAsync() ?? Task.FromResult(new List<Attraction>()));

            var isUserLocked = User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(currentUserId);
            if (!isUserLocked)
            {
                var users = await _context.Users
                    .AsNoTracking()
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
                ViewData["UserId"] = new SelectList(users, "Id", "UserName", review.UserId);
            }

            ViewData["AttractionId"] = new SelectList(attractions, "Id", "Name", review.AttractionId);
            ViewBag.UserLocked = isUserLocked;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.ReturnToAttraction = returnToAttraction;
            ViewBag.AttractionId = review.AttractionId;
            return View(review);
        }

        // GET: Reviews/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var review = await _context.Reviews!.FindAsync(id.Value);
            if (review == null) return NotFound();

            var attractions = await (_context.Attractions?.OrderBy(a => a.Name).ToListAsync() ?? Task.FromResult(new List<Attraction>()));
            var users = await (_context.Users.OrderBy(u => u.UserName).ToListAsync());

            ViewData["AttractionId"] = new SelectList(attractions, "Id", "Name", review.AttractionId);
            ViewData["UserId"] = new SelectList(users, "Id", "UserName", review.UserId);
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(review);
        }

        // POST: Reviews/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Comment,Rating,CreatedAt,UserId,AttractionId,IsApproved,IsRejected")] Review review)
        {
            if (id != review.Id) return NotFound();

            ModelState.Remove(nameof(Review.User));
            ModelState.Remove(nameof(Review.Attraction));

            if (review.Rating < 1 || review.Rating > 5)
            {
                ModelState.AddModelError(nameof(Review.Rating), "Puan 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingReview = await _context.Reviews!.FirstOrDefaultAsync(r => r.Id == id);
                    if (existingReview == null) return NotFound();

                    existingReview.Comment = review.Comment;
                    existingReview.Rating = review.Rating;
                    existingReview.CreatedAt = review.CreatedAt;
                    existingReview.UserId = review.UserId;
                    existingReview.AttractionId = review.AttractionId;

                    if (User.IsInRole("Admin"))
                    {
                        existingReview.IsApproved = review.IsApproved;
                        existingReview.IsRejected = review.IsRejected;
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReviewExists(review.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var attractions = await (_context.Attractions?.OrderBy(a => a.Name).ToListAsync() ?? Task.FromResult(new List<Attraction>()));
            var users = await (_context.Users.OrderBy(u => u.UserName).ToListAsync());

            ViewData["AttractionId"] = new SelectList(attractions, "Id", "Name", review.AttractionId);
            ViewData["UserId"] = new SelectList(users, "Id", "UserName", review.UserId);
            ViewBag.IsAdmin = User.IsInRole("Admin");
            return View(review);
        }

        // GET: Reviews/Delete/5
        public async Task<IActionResult> Delete(int? id, bool returnToAttraction = false)
        {
            if (id == null) return NotFound();

            var review = await _context.Reviews!
                .Include(r => r.Attraction)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id.Value);
            if (review == null) return NotFound();

            ViewBag.ReturnToAttraction = returnToAttraction;
            ViewBag.AttractionId = review.AttractionId;

            return View(review);
        }

        // POST: Reviews/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool returnToAttraction = false)
        {
            var review = await _context.Reviews!.FindAsync(id);
            var attractionId = review?.AttractionId;

            if (review != null)
            {
                _context.Reviews!.Remove(review);
                await _context.SaveChangesAsync();
            }

            if (returnToAttraction && attractionId.HasValue)
            {
                return RedirectToAction("Details", "Attractions", new { id = attractionId.Value });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id, string? returnUrl = null)
        {
            var review = await _context.Reviews!.FirstOrDefaultAsync(r => r.Id == id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = true;
            review.IsRejected = false;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Yorum onaylandı.";

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
            var review = await _context.Reviews!.FirstOrDefaultAsync(r => r.Id == id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsApproved = false;
            review.IsRejected = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Yorum reddedildi.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ReviewExists(int id)
        {
            return _context.Reviews!.Any(e => e.Id == id);
        }
    }
}