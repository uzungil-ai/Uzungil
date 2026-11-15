using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Models;
using System.Linq;

namespace Roma.Pages.Account
{
    public class VerifyAuthenticatorSignInModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;

        public VerifyAuthenticatorSignInModel(UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public string Code { get; set; } = null!;

        [BindProperty]
        public bool RememberMe { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            var safeReturnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/");

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                ErrorMessage = "İşlem süresi geçti. Lütfen tekrar giriş yapın.";
                return Page();
            }

            var normalizedCode = NormalizeVerificationCode(Code);
            if (string.IsNullOrWhiteSpace(normalizedCode) || normalizedCode.Length != 6)
            {
                ErrorMessage = "Authenticator uygulamasındaki 6 haneli kodu girin.";
                return Page();
            }

            var signInResult = await _signInManager.TwoFactorAuthenticatorSignInAsync(normalizedCode, RememberMe, rememberClient: false);
            if (!signInResult.Succeeded)
            {
                ErrorMessage = "Kod doğrulanamadı. Kodu yenileyip tekrar deneyin ve cihaz saatinin otomatik ayarlı olduğundan emin olun.";
                return Page();
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["ShowPendingReviewsPopup"] = "1";
            }

            return LocalRedirect(safeReturnUrl!);
        }

        private static string NormalizeVerificationCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return new string(code.Where(char.IsDigit).ToArray());
        }
    }
}
