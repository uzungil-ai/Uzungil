using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Models;

namespace Roma.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;

        public LoginWith2faModel(SignInManager<User> signInManager, UserManager<User> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool RememberMe { get; set; }
        public string ReturnUrl { get; set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "Kod gerekli.")]
            [Display(Name = "Authenticator Kodu")]
            public string TwoFactorCode { get; set; } = string.Empty;

            [Display(Name = "Bu cihazı hatırla")]
            public bool RememberMachine { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(bool rememberMe, string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "" });
            }

            RememberMe = rememberMe;
            ReturnUrl = returnUrl ?? Url.Content("~/");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                RememberMe = rememberMe;
                ReturnUrl = returnUrl;
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "" });
            }

            var authenticatorCode = NormalizeVerificationCode(Input.TwoFactorCode);
            if (authenticatorCode.Length != 6)
            {
                ModelState.AddModelError(string.Empty, "6 haneli authenticator kodu girin.");
                RememberMe = rememberMe;
                ReturnUrl = returnUrl;
                return Page();
            }

            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, rememberMe, Input.RememberMachine);

            if (result.Succeeded)
            {
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    TempData["ShowPendingReviewsPopup"] = "1";
                }
                return LocalRedirect(returnUrl);
            }

            // Bazı ortamlarda TwoFactorAuthenticatorSignInAsync başarısız dönebilir.
            // Kod gerçekten doğruysa manuel doğrulama + giriş ile devam et.
            var tokenValid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, authenticatorCode);
            if (tokenValid)
            {
                await _signInManager.SignInAsync(user, rememberMe);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                {
                    TempData["ShowPendingReviewsPopup"] = "1";
                }

                if (Input.RememberMachine)
                {
                    await _signInManager.RememberTwoFactorClientAsync(user);
                }

                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Geçersiz authenticator kodu.");
            RememberMe = rememberMe;
            ReturnUrl = returnUrl;
            return Page();
        }

        private static string NormalizeVerificationCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return new string(code.Where(char.IsDigit).ToArray());
        }
    }
}
