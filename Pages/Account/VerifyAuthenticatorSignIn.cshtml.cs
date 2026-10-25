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

        [BindProperty]
        public string UserId { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            if (TempData.ContainsKey("TwoFactorUserId"))
            {
                UserId = TempData["TwoFactorUserId"] as string ?? string.Empty;
            }

            if (TempData.ContainsKey("RememberMe"))
            {
                bool.TryParse(TempData["RememberMe"] as string, out var remember);
                RememberMe = remember;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UserId))
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

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                ErrorMessage = "Kullanıcı bulunamadı.";
                return Page();
            }

            // verify authenticator token
            var valid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, normalizedCode);
            if (!valid)
            {
                ErrorMessage = "Kod doğrulanamadı. Kodu yenileyip tekrar deneyin ve cihaz saatinin otomatik ayarlı olduğundan emin olun.";
                return Page();
            }

            await _signInManager.SignInAsync(user, RememberMe);
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["ShowPendingReviewsPopup"] = "1";
            }
            return LocalRedirect("/");
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
