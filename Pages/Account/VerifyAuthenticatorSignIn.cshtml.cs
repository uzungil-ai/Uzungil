using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Models;

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

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            if (TempData.ContainsKey("RememberMe"))
            {
                bool.TryParse(TempData["RememberMe"] as string, out var remember);
                RememberMe = remember;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = TempData["TwoFactorUserId"] as string;
            if (string.IsNullOrEmpty(userId))
            {
                ErrorMessage = "İşlem süresi geçti. Lütfen tekrar giriş yapın.";
                return Page();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ErrorMessage = "Kullanıcı bulunamadı.";
                return Page();
            }

            // verify authenticator token
            var valid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, Code);
            if (!valid)
            {
                ErrorMessage = "Kod hatalı.";
                return Page();
            }

            await _signInManager.SignInAsync(user, RememberMe);
            return LocalRedirect("/");
        }
    }
}
