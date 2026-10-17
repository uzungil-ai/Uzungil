using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Models;

namespace Roma.Pages.Account
{
    public class EnableAuthenticatorModel : PageModel
    {
        private readonly UserManager<User> _userManager;

        public EnableAuthenticatorModel(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public string SharedKey { get; set; } = null!;
        public string QrCodeUrl { get; set; } = null!;
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            SharedKey = FormatKey(key);
            var email = await _userManager.GetEmailAsync(user);
            var otpauth = $"otpauth://totp/{Uri.EscapeDataString("Roma")}:{Uri.EscapeDataString(email)}?secret={key}&issuer={Uri.EscapeDataString("Roma")}&digits=6";
            QrCodeUrl = $"https://chart.googleapis.com/chart?chs=200x200&chld=M|0&cht=qr&chl={Uri.EscapeDataString(otpauth)}";
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string Code)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                ErrorMessage = "Authenticator anahtarı bulunamadı.";
                return Page();
            }

            var valid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, Code);
            if (!valid)
            {
                ErrorMessage = "Doğrulama başarısız.";
                return await OnGetAsync();
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            return RedirectToPage("/Index");
        }

        private static string FormatKey(string unformattedKey)
        {
            // group in blocks of 4 for readability
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
