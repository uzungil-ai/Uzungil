using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using Roma.Models;
using System.Linq;
using System.Text;

namespace Roma.Pages.Account
{
    [Authorize]
    public class EnableAuthenticatorModel : PageModel
    {
        private const string AuthenticatorIssuer = "Roma";
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

            await LoadSharedKeyAndQrCodeUriAsync(user);
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

            var normalizedCode = NormalizeVerificationCode(Code);
            if (string.IsNullOrWhiteSpace(normalizedCode) || normalizedCode.Length != 6)
            {
                ErrorMessage = "Lütfen Authenticator uygulamasındaki 6 haneli kodu girin.";
                return await OnGetAsync();
            }

            var valid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, normalizedCode);
            if (!valid)
            {
                ErrorMessage = "Doğrulama başarısız. Uygulamadaki 6 haneli anlık kodu deneyin ve cihaz saatinin otomatik senkronize olduğundan emin olun.";
                return await OnGetAsync();
            }

            var enableResult = await _userManager.SetTwoFactorEnabledAsync(user, true);
            if (!enableResult.Succeeded)
            {
                ErrorMessage = "Authenticator etkinleştirilemedi. " + string.Join(" ", enableResult.Errors.Select(e => e.Description));
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            // Safety check
            var enabled = await _userManager.GetTwoFactorEnabledAsync(user);
            if (!enabled)
            {
                ErrorMessage = "Authenticator doğrulandı ancak 2FA işaretlenemedi. Lütfen tekrar deneyin.";
                await LoadSharedKeyAndQrCodeUriAsync(user);
                return Page();
            }

            return RedirectToPage("/Index");
        }

        private static string FormatKey(string unformattedKey)
        {
            // group in blocks of 4 for readability
            var result = new StringBuilder();
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

        private async Task LoadSharedKeyAndQrCodeUriAsync(User user)
        {
            var key = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                key = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            if (string.IsNullOrEmpty(key))
            {
                SharedKey = string.Empty;
                QrCodeUrl = string.Empty;
                ErrorMessage = "Authenticator anahtarı üretilemedi. Lütfen tekrar deneyin.";
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
