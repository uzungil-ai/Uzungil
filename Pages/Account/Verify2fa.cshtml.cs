using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Account
{
    public class Verify2faModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;

        public Verify2faModel(ApplicationDbContext db, SignInManager<User> signInManager, UserManager<User> userManager)
        {
            _db = db;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public string Id { get; set; } = null!;
        [BindProperty]
        public bool RememberMe { get; set; }
        [BindProperty]
        public string Code { get; set; } = null!;

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            // read id and remember from TempData
            if (TempData.ContainsKey("TwoFactorId"))
            {
                Id = TempData["TwoFactorId"] as string ?? string.Empty;
            }
            if (TempData.ContainsKey("RememberMe"))
            {
                bool.TryParse(TempData["RememberMe"] as string, out var remember);
                RememberMe = remember;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Id) || string.IsNullOrEmpty(Code))
            {
                ErrorMessage = "Kod gerekli.";
                return Page();
            }

            var tf = await _db.TwoFactorCodes!.FirstOrDefaultAsync(t => t.Id == Id);
            if (tf == null || tf.Used || tf.ExpiresAt < DateTime.UtcNow)
            {
                ErrorMessage = "Kod geçersiz veya süresi dolmuş.";
                return Page();
            }

            // verify provided code by hashing with stored salt
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashInput = System.Text.Encoding.UTF8.GetBytes(tf.Salt + Code);
            var hashed = Convert.ToBase64String(sha.ComputeHash(hashInput));
            if (hashed != tf.HashedCode)
            {
                // increment attempts
                tf.Attempts++;
                if (tf.Attempts >= 5)
                {
                    tf.Used = true; // lock out this code
                }
                await _db.SaveChangesAsync();
                ErrorMessage = "Kod hatalı.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(tf.UserId);
            if (user == null)
            {
                ErrorMessage = "Kullanıcı bulunamadı.";
                return Page();
            }

            tf.Used = true;
            await _db.SaveChangesAsync();

            await _signInManager.SignInAsync(user, RememberMe);

            return LocalRedirect("/");
        }
    }
}
