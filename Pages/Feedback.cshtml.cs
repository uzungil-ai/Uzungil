using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages
{
    public class FeedbackModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public FeedbackModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string? Email { get; set; }
        [BindProperty]
        public string Message { get; set; } = null!;

        public bool Success { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Message))
            {
                ModelState.AddModelError("", "Mesaj boş olamaz.");
                return Page();
            }

            var fb = new Feedback { Email = Email, Message = Message };
            _db.Feedbacks!.Add(fb);
            await _db.SaveChangesAsync();

            Success = true;
            return Page();
        }
    }
}
