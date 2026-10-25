using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;

namespace Roma.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly Roma.Services.IEmailQueue? _queue;
        private readonly bool _twoFactorEnabled;

        public LoginModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext db,
            IConfiguration configuration,
            Roma.Services.IEmailQueue? queue = null)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _queue = queue;
            _twoFactorEnabled = configuration.GetValue("Features:TwoFactorEnabled", false);
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            public string Email { get; set; } = null!;
            public string Password { get; set; } = null!;
            public bool RememberMe { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Input.Email) || string.IsNullOrWhiteSpace(Input.Password))
            {
                ErrorMessage = "E-posta ve şifre gereklidir.";
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ErrorMessage = "Geçersiz giriş.";
                return Page();
            }

            // verify password
            var passwordValid = await _userManager.CheckPasswordAsync(user, Input.Password);
            if (!passwordValid)
            {
                ErrorMessage = "Geçersiz giriş.";
                return Page();
            }

            if (!_twoFactorEnabled)
            {
                await _signInManager.SignInAsync(user, Input.RememberMe);
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    TempData["ShowPendingReviewsPopup"] = "1";
                }
                return LocalRedirect("/");
            }

            // rate limiting: max 3 codes per hour
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var recentCount = await _db.TwoFactorCodes!.Where(t => t.UserId == user.Id && t.CreatedAt >= oneHourAgo).CountAsync();
            if (recentCount >= 3)
            {
                ErrorMessage = "Çok fazla kod talebi. Lütfen bir saat sonra tekrar deneyin.";
                return Page();
            }

            // if user has authenticator app enabled, use authenticator flow instead of email OTP
            var isAuthenticatorEnabled = await _userManager.GetTwoFactorEnabledAsync(user) && !string.IsNullOrEmpty(await _userManager.GetAuthenticatorKeyAsync(user));
            if (isAuthenticatorEnabled)
            {
                TempData["TwoFactorUserId"] = user.Id;
                TempData["RememberMe"] = Input.RememberMe.ToString();
                return RedirectToPage("/Account/VerifyAuthenticatorSignIn");
            }

            // create two-factor code (6 digits)
            var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var num = System.BitConverter.ToUInt32(bytes, 0) % 900000 + 100000;
            var code = num.ToString();

            // generate salt and hash
            var saltBytes = new byte[16];
            rng.GetBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hashInput = System.Text.Encoding.UTF8.GetBytes(salt + code);
            var hashed = Convert.ToBase64String(sha.ComputeHash(hashInput));

            var tf = new TwoFactorCode
            {
                UserId = user.Id,
                HashedCode = hashed,
                Salt = salt,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                Used = false,
                Attempts = 0
            };
            _db.TwoFactorCodes!.Add(tf);
            await _db.SaveChangesAsync();

            // send code via email
            try
            {
                var body = $"Giriş kodunuz: {code}. Bu kod 10 dakika içinde geçerlidir.";
                if (_queue != null)
                {
                    await _queue.EnqueueAsync(new Roma.Services.EmailMessage(user.Email!, "Giriş doğrulama kodu", body));
                }
            }
            catch
            {
            }

            // store 2fa id in TempData and redirect to verify page
            TempData["TwoFactorId"] = tf.Id;
            TempData["RememberMe"] = Input.RememberMe.ToString();
            return RedirectToPage("/Account/Verify2fa");
        }
    }
}
