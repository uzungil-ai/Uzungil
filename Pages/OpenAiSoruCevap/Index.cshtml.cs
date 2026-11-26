using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Roma.Pages.OpenAiSoruCevap
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            return RedirectToPage("/Chat/Index");
        }

        public IActionResult OnPostAsk()
        {
            return new JsonResult(new { redirectUrl = "/Chat" });
        }
    }
}