using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using Roma.Models;

namespace Roma.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginWith2faModel : PageModel
    {
        private const string AuthenticatorIssuer = "Roma";
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
        public string QrCodeUrl { get; set; } = string.Empty;
        public string SharedKey { get; set; } = string.Empty;

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
            await LoadQrAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                var invalidUser = await _signInManager.GetTwoFactorAuthenticationUserAsync();
                if (invalidUser != null)
                {
                    await LoadQrAsync(invalidUser);
                }
                RememberMe = rememberMe;
                ReturnUrl = returnUrl;
                return Page();
            }

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { area = "" });
            }

            var authenticatorCode = (Input.TwoFactorCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
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
            await LoadQrAsync(user);
            RememberMe = rememberMe;
            ReturnUrl = returnUrl;
            return Page();
        }

        private async Task LoadQrAsync(User user)
        {
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrWhiteSpace(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                QrCodeUrl = string.Empty;
                SharedKey = string.Empty;
                return;
            }

            SharedKey = FormatKey(key);
            var email = await _userManager.GetEmailAsync(user);
            var accountName = string.IsNullOrWhiteSpace(email) ? user.UserName ?? user.Id : email;
            var otpauth = GenerateQrCodeUri(accountName, key);
            QrCodeUrl = BuildLocalQrCodeDataUrl(otpauth);
        }

        private static string BuildLocalQrCodeDataUrl(string payload)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var pngQr = new PngByteQRCode(data);
            var bytes = pngQr.GetGraphic(20);
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }

        private static string GenerateQrCodeUri(string accountName, string unformattedKey)
        {
            var encodedIssuer = Uri.EscapeDataString(AuthenticatorIssuer);
            var encodedAccountName = Uri.EscapeDataString(accountName);
            return $"otpauth://totp/{encodedIssuer}:{encodedAccountName}?secret={unformattedKey}&issuer={encodedIssuer}&digits=6&period=30&algorithm=SHA1";
        }

        private static string FormatKey(string unformattedKey)
        {
            var result = new System.Text.StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }

            if (currentPosition < unformattedKey.Length)
            {
                result.Append(unformattedKey.Substring(currentPosition));
            }

            return result.ToString().ToUpperInvariant();
        }
    }
}
