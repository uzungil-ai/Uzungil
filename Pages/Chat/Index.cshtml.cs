using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Roma.Services;

namespace Roma.Pages.Chat
{
    public class IndexModel : PageModel
    {
        private readonly IChatService _chat;

        public IndexModel(IChatService chat)
        {
            _chat = chat;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostSendAsync([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Text)) return BadRequest();
            var reply = await _chat.GetReplyAsync(req.Text, HttpContext.Session.Id);
            return new JsonResult(new { reply });
        }

        public class ChatRequest { public string Text { get; set; } = string.Empty; }
    }
}
