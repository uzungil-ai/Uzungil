using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.ViewComponents
{
    public class PendingReviewsPopupViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _dbContext;

        public PendingReviewsPopupViewComponent(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IViewComponentResult> InvokeAsync(string currentPathAndQuery)
        {
            var pendingReviews = await _dbContext.Reviews!
                .AsNoTracking()
                .Where(r => !r.IsApproved && !r.IsRejected)
                .Include(r => r.User)
                .Include(r => r.Attraction)
                .OrderBy(r => r.CreatedAt)
                .Take(10)
                .ToListAsync();

            if (pendingReviews.Count == 0)
            {
                return Content(string.Empty);
            }

            var model = new PendingReviewsPopupViewModel
            {
                CurrentPathAndQuery = currentPathAndQuery,
                PendingReviews = pendingReviews
            };

            return View(model);
        }
    }

    public class PendingReviewsPopupViewModel
    {
        public required string CurrentPathAndQuery { get; set; }
        public List<Review> PendingReviews { get; set; } = new();
    }
}