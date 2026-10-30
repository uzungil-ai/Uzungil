using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Roma.Models;

namespace Roma.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly bool _requireConfirmedAccount;

        public LoginModel(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IOptions<IdentityOptions> identityOptions,
            ILogger<LoginModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _requireConfirmedAccount = identityOptions.Value.SignIn.RequireConfirmedAccount;
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
            var loginIdentifier = (Input.Email ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(loginIdentifier) || string.IsNullOrWhiteSpace(Input.Password))
            {
                ErrorMessage = "E-posta ve şifre gereklidir.";
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(loginIdentifier)
                       ?? await _userManager.FindByNameAsync(loginIdentifier);

            if (user == null)
            {
                _logger.LogWarning("Login failed: user not found for '{LoginIdentifier}'.", loginIdentifier);
                ErrorMessage = "Geçersiz giriş.";
                return Page();
            }

            if (_requireConfirmedAccount && !await _userManager.IsEmailConfirmedAsync(user))
            {
                _logger.LogInformation("Login blocked: unconfirmed account for user '{UserId}'.", user.Id);
                ErrorMessage = "Hesabınız henüz onaylanmamış. Lütfen e-posta onayını tamamlayın.";
                return Page();
            }

            var signInResult = await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
            if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("Login blocked: user '{UserId}' is locked out.", user.Id);
                ErrorMessage = "Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.";
                return Page();
            }

            if (signInResult.IsNotAllowed)
            {
                _logger.LogInformation("Login blocked: user '{UserId}' is not allowed to sign in.", user.Id);
                ErrorMessage = "Hesabınız şu an girişe uygun değil.";
                return Page();
            }

            if (signInResult.RequiresTwoFactor)
            {
                _logger.LogInformation("Two-factor temporarily bypassed for user '{UserId}'.", user.Id);
                await _signInManager.SignInAsync(user, Input.RememberMe);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    TempData["ShowPendingReviewsPopup"] = "1";
                }

                return LocalRedirect("/");
            }

            if (signInResult.Succeeded)
            {
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    TempData["ShowPendingReviewsPopup"] = "1";
                }
                return LocalRedirect("/");
            }

            _logger.LogWarning("Login failed: invalid password for user '{UserId}'.", user.Id);
            ErrorMessage = "Geçersiz giriş.";
            return Page();
        }
    }
}
