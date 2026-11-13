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
                .Where(r => r.IsApproved && !r.IsRejected)
                .Include(r => r.Attraction)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
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
        public async Task<IActionResult> Create(int? attractionId, string? returnUrl = null)
        {
            var localReturnUrl = ResolveLocalReturnUrl(returnUrl);
            var destinationId = await ResolveDestinationIdAsync(attractionId, localReturnUrl);

            if (destinationId.HasValue)
            {
                await EnsureAttractionsForDestinationAsync(destinationId.Value);
            }

            var attractionsQuery = (_context.Attractions?
                .AsNoTracking()
                .AsQueryable()) ?? Enumerable.Empty<Attraction>().AsQueryable();

            if (destinationId.HasValue)
            {
                attractionsQuery = attractionsQuery.Where(a => a.DestinationId == destinationId.Value);
            }

            var attractions = await attractionsQuery
                .OrderBy(a => a.Name)
                .ToListAsync();

            var selectedAttractionId = attractionId ?? attractions.FirstOrDefault()?.Id;

            var destinationName = destinationId.HasValue
                ? await _context.Destinations!
                    .AsNoTracking()
                    .Where(d => d.Id == destinationId.Value)
                    .Select(d => d.Name)
                    .FirstOrDefaultAsync()
                : null;

            var selectedAttractionName = selectedAttractionId.HasValue
                ? attractions.FirstOrDefault(a => a.Id == selectedAttractionId.Value)?.Name
                : null;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isUserLocked = User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(currentUserId);

            ViewData["AttractionId"] = new SelectList(attractions, "Id", "Name", selectedAttractionId);
            ViewBag.Attractions = attractions;
            ViewBag.SelectedAttractionIds = attractions.Select(a => a.Id).ToList();
            ViewBag.DestinationName = destinationName;
            ViewBag.SelectedAttractionName = selectedAttractionName;
            ViewBag.UserLocked = isUserLocked;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.GuestEmail = string.Empty;
            ViewBag.ShowSubscribeButton = false;

            ViewBag.ReturnToAttraction = attractionId.HasValue;
            ViewBag.ReturnAttractionId = selectedAttractionId;
            ViewBag.ReturnUrl = localReturnUrl;
            ViewBag.BulkReviews = attractions
                .Select(a => new ReviewBulkInput { AttractionId = a.Id, IsSelected = true, Rating = 5, Comment = string.Empty })
                .ToList();

            return View(new Review
            {
                Comment = string.Empty,
                Rating = 5,
                UserId = currentUserId ?? string.Empty,
                AttractionId = selectedAttractionId ?? 0,
                User = null!,
                Attraction = null!
            });
        }

        // POST: Reviews/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Comment,Rating,UserId,AttractionId")] Review review, List<ReviewBulkInput>? bulkReviews, string? guestEmail, bool returnToAttraction = false, string? returnUrl = null)
        {
            var localReturnUrl = ResolveLocalReturnUrl(returnUrl);
            var destinationId = await ResolveDestinationIdAsync(returnToAttraction ? review.AttractionId : null, localReturnUrl);

            if (destinationId.HasValue)
            {
                await EnsureAttractionsForDestinationAsync(destinationId.Value);
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(currentUserId))
            {
                var signedInUser = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == currentUserId);

                if (signedInUser?.EmailConfirmed == true)
                {
                    review.UserId = currentUserId;
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Yorum eklemek için e-posta adresinizi doğrulamanız gerekir.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(guestEmail))
                {
                    ModelState.AddModelError("guestEmail", "Yorum eklemek için kayıtlı e-posta adresinizi giriniz.");
                }
                else
                {
                    var guestEmailTrimmed = guestEmail.Trim();
                    var normalizedEmail = guestEmailTrimmed.ToUpperInvariant();
                    var confirmedSubscriberExists = await _context.Subscribers!
                        .AsNoTracking()
                        .AnyAsync(s => s.IsConfirmed && s.ConsentGiven && s.Email.ToUpper() == normalizedEmail);

                    if (!confirmedSubscriberExists)
                    {
                        ModelState.AddModelError("guestEmail", "Bu e-posta için aktif abonelik bulunamadı. Lütfen yeniden abone olun.");
                        ViewBag.ShowSubscribeButton = true;
                    }

                    var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
                    if (existingUser == null && confirmedSubscriberExists)
                    {
                        var newUser = new User
                        {
                            Id = Guid.NewGuid().ToString(),
                            UserName = guestEmailTrimmed,
                            NormalizedUserName = normalizedEmail,
                            Email = guestEmailTrimmed,
                            NormalizedEmail = normalizedEmail,
                            EmailConfirmed = true,
                            SecurityStamp = Guid.NewGuid().ToString("N"),
                            ConcurrencyStamp = Guid.NewGuid().ToString("N")
                        };

                        _context.Users.Add(newUser);
                        review.UserId = newUser.Id;
                    }
                    else if (existingUser != null)
                    {
                        if (existingUser.EmailConfirmed && confirmedSubscriberExists)
                        {
                            review.UserId = existingUser.Id;
                        }
                        else
                        {
                            ModelState.AddModelError("guestEmail", "Bu e-posta için aktif abonelik bulunamadı veya hesap doğrulanmamış.");
                            ViewBag.ShowSubscribeButton = true;
                        }
                    }
                }
            }

            ModelState.Remove(nameof(Review.UserId));
            ModelState.Remove(nameof(Review.AttractionId));
            ModelState.Remove(nameof(Review.User));
            ModelState.Remove(nameof(Review.Attraction));

            if (!returnToAttraction)
            {
                ModelState.Remove(nameof(Review.Comment));
                ModelState.Remove(nameof(Review.Rating));
            }

            var targetAttractionIds = new List<int>();
            var selectedBulkReviews = new List<ReviewBulkInput>();
            if (returnToAttraction)
            {
                if (review.AttractionId > 0)
                {
                    targetAttractionIds.Add(review.AttractionId);
                }
            }
            else
            {
                selectedBulkReviews = (bulkReviews ?? new List<ReviewBulkInput>())
                    .Where(x => x.IsSelected && x.AttractionId > 0)
                    .ToList();

                if (selectedBulkReviews.Count == 0)
                {
                    ModelState.AddModelError(nameof(Review.AttractionId), "Lütfen en az bir başlık seçiniz.");
                }
                else
                {
                    targetAttractionIds.AddRange(selectedBulkReviews.Select(x => x.AttractionId));

                    foreach (var item in selectedBulkReviews)
                    {
                        if (string.IsNullOrWhiteSpace(item.Comment))
                        {
                            ModelState.AddModelError(nameof(Review.Comment), "Seçili başlıklar için yorum metni zorunludur.");
                            break;
                        }

                        if (item.Rating < 1 || item.Rating > 5)
                        {
                            ModelState.AddModelError(nameof(Review.Rating), "Puan 1 ile 5 arasında olmalıdır.");
                            break;
                        }
                    }
                }
            }

            targetAttractionIds = targetAttractionIds.Distinct().ToList();

            if (targetAttractionIds.Count == 0)
            {
                ModelState.AddModelError(nameof(Review.AttractionId), "Lütfen en az bir başlık seçiniz.");
            }
            else if (destinationId.HasValue)
            {
                var validAttractionIds = await _context.Attractions!
                    .AsNoTracking()
                    .Where(a => a.DestinationId == destinationId.Value)
                    .Select(a => a.Id)
                    .ToListAsync();

                var invalidSelectionExists = targetAttractionIds.Any(id => !validAttractionIds.Contains(id));
                if (invalidSelectionExists)
                {
                    ModelState.AddModelError(nameof(Review.AttractionId), "Yalnızca bu destinasyondaki görülecek yerler için yorum ekleyebilirsiniz.");
                }
            }

            if (returnToAttraction && (review.Rating < 1 || review.Rating > 5))
            {
                ModelState.AddModelError(nameof(Review.Rating), "Puan 1 ile 5 arasında olmalıdır.");
            }

            if (ModelState.IsValid)
            {
                var now = DateTime.UtcNow;

                if (returnToAttraction)
                {
                    var newReview = new Review
                    {
                        Comment = review.Comment,
                        Rating = review.Rating,
                        UserId = review.UserId,
                        AttractionId = review.AttractionId,
                        CreatedAt = now,
                        IsApproved = false,
                        IsRejected = false,
                        User = null!,
                        Attraction = null!
                    };

                    _context.Add(newReview);
                }
                else
                {
                    foreach (var item in selectedBulkReviews)
                    {
                        var newReview = new Review
                        {
                            Comment = item.Comment,
                            Rating = item.Rating,
                            UserId = review.UserId,
                            AttractionId = item.AttractionId,
                            CreatedAt = now,
                            IsApproved = false,
                            IsRejected = false,
                            User = null!,
                            Attraction = null!
                        };

                        _context.Add(newReview);
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Yorumunuz alındı. Yönetici onayından sonra yayınlanacaktır.";
                if (!string.IsNullOrEmpty(localReturnUrl))
                {
                    return Redirect(localReturnUrl);
                }

                if (returnToAttraction)
                {
                    return RedirectToAction("Details", "Attractions", new { id = review.AttractionId });
                }
                return RedirectToAction(nameof(Index));
            }

            var attractionsQuery = (_context.Attractions?
                .AsNoTracking()
                .AsQueryable()) ?? Enumerable.Empty<Attraction>().AsQueryable();

            if (destinationId.HasValue)
            {
                attractionsQuery = attractionsQuery.Where(a => a.DestinationId == destinationId.Value);
            }

            var attractions = await attractionsQuery
                .OrderBy(a => a.Name)
                .ToListAsync();

            var selectedAttractionId = review.AttractionId > 0
                ? review.AttractionId
                : attractions.FirstOrDefault()?.Id;

            var selectedIdsForRetry = (bulkReviews ?? new List<ReviewBulkInput>())
                .Where(x => x.IsSelected && x.AttractionId > 0)
                .Select(x => x.AttractionId)
                .Distinct()
                .ToList();
            if (!returnToAttraction && selectedIdsForRetry.Count == 0)
            {
                selectedIdsForRetry = attractions.Select(a => a.Id).ToList();
            }

            var bulkReviewsForRetry = attractions
                .Select(a =>
                {
                    var existing = (bulkReviews ?? new List<ReviewBulkInput>()).FirstOrDefault(x => x.AttractionId == a.Id);
                    return new ReviewBulkInput
                    {
                        AttractionId = a.Id,
                        IsSelected = existing?.IsSelected ?? selectedIdsForRetry.Contains(a.Id),
                        Comment = existing?.Comment ?? string.Empty,
                        Rating = existing?.Rating is >= 1 and <= 5 ? existing.Rating : 5
                    };
                })
                .ToList();

            var destinationName = destinationId.HasValue
                ? await _context.Destinations!
                    .AsNoTracking()
                    .Where(d => d.Id == destinationId.Value)
                    .Select(d => d.Name)
                    .FirstOrDefaultAsync()
                : null;

            var selectedAttractionName = selectedAttractionId.HasValue
                ? attractions.FirstOrDefault(a => a.Id == selectedAttractionId.Value)?.Name
                : null;

            var isUserLocked = User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(currentUserId);

            ViewData["AttractionId"] = new SelectList(attractions, "Id", "Name", selectedAttractionId);
            ViewBag.Attractions = attractions;
            ViewBag.SelectedAttractionIds = selectedIdsForRetry;
            ViewBag.BulkReviews = bulkReviewsForRetry;
            ViewBag.DestinationName = destinationName;
            ViewBag.SelectedAttractionName = selectedAttractionName;
            ViewBag.UserLocked = isUserLocked;
            ViewBag.CurrentUserId = currentUserId;
            ViewBag.GuestEmail = guestEmail;
            ViewBag.ShowSubscribeButton ??= false;
            ViewBag.ReturnToAttraction = returnToAttraction;
            ViewBag.ReturnAttractionId = selectedAttractionId;
            ViewBag.ReturnUrl = localReturnUrl;

            if (returnToAttraction && review.Rating <= 0)
            {
                review.Rating = 5;
            }

            return View(review);
        }

        private async Task<int?> ResolveDestinationIdAsync(int? attractionId, string? localReturnUrl)
        {
            if (attractionId.HasValue && attractionId.Value > 0)
            {
                var attractionDestinationId = await _context.Attractions!
                    .AsNoTracking()
                    .Where(a => a.Id == attractionId.Value)
                    .Select(a => (int?)a.DestinationId)
                    .FirstOrDefaultAsync();

                if (attractionDestinationId.HasValue)
                {
                    return attractionDestinationId;
                }
            }

            if (string.IsNullOrWhiteSpace(localReturnUrl))
            {
                return null;
            }

            var path = localReturnUrl.Split('?', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd('/');

            var photoPrefix = "/Destinations/PhotoDetails/";
            if (path.StartsWith(photoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var idPart = path.Substring(photoPrefix.Length);
                if (int.TryParse(idPart, out var destinationIdFromPhoto))
                {
                    return destinationIdFromPhoto;
                }
            }

            return null;
        }

        private async Task EnsureAttractionsForDestinationAsync(int destinationId)
        {
            var destination = await _context.Destinations!
                .Include(d => d.Attractions)
                .Include(d => d.Hotels)
                .Include(d => d.Meals)
                .Include(d => d.Flavors)
                .Include(d => d.Parkings)
                .FirstOrDefaultAsync(d => d.Id == destinationId);

            if (destination == null)
            {
                return;
            }

            var existingNames = new HashSet<string>(
                (destination.Attractions ?? new List<Attraction>())
                .Select(a => (a.Name ?? string.Empty).Trim().ToUpperInvariant())
                .Where(n => !string.IsNullOrWhiteSpace(n)));

            void AddAttractionIfMissing(string? name, string category, string? address = null)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var normalizedName = name.Trim().ToUpperInvariant();
                if (existingNames.Contains(normalizedName))
                {
                    return;
                }

                _context.Attractions!.Add(new Attraction
                {
                    Name = name.Trim(),
                    Category = category,
                    Address = string.IsNullOrWhiteSpace(address) ? "Adres belirtilmedi" : address.Trim(),
                    DestinationId = destinationId,
                    Destination = null!
                });

                existingNames.Add(normalizedName);
            }

            foreach (var item in destination.Hotels ?? new List<Hotel>())
            {
                AddAttractionIfMissing(item.Name, "Otel, Pansiyon, Misafirhane", item.Address);
            }

            foreach (var item in destination.Meals ?? new List<Meal>())
            {
                AddAttractionIfMissing(item.Name, "Restoran, Cafe", item.Address);
            }

            foreach (var item in destination.Flavors ?? new List<Flavor>())
            {
                AddAttractionIfMissing(item.Name, "Yöresel Yemekler");
            }

            foreach (var item in destination.Parkings ?? new List<Parking>())
            {
                AddAttractionIfMissing(item.Name, "Otoparklar");
            }

            await _context.SaveChangesAsync();
        }

        private string? ResolveLocalReturnUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            var referer = Request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                var localPath = refererUri.PathAndQuery;
                if (Url.IsLocalUrl(localPath))
                {
                    return localPath;
                }
            }

            return null;
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