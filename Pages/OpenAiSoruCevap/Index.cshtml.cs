using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Data;
using Roma.Services;

namespace Roma.Pages.OpenAiSoruCevap
{
    public class IndexModel : PageModel
    {
        private readonly IOpenAiQaService _qaService;
        private readonly ApplicationDbContext _db;
        private readonly int _maxInputChars;
        private readonly int _maxOutputChars;

        public IndexModel(IOpenAiQaService qaService, ApplicationDbContext db, IConfiguration configuration)
        {
            _qaService = qaService;
            _db = db;
            _maxInputChars = configuration.GetValue<int?>("AiBudget:MaxInputChars") ?? 500;
            _maxOutputChars = configuration.GetValue<int?>("AiBudget:MaxOutputChars") ?? 400;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAskAsync([FromBody] QaRequest request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest();
            }

            if (request.Text.Length > _maxInputChars)
            {
                return new JsonResult(new { reply = $"Sorunuz çok uzun. En fazla {_maxInputChars} karakter kullanın." });
            }

            var reply = await _qaService.AskAsync(request.Text.Trim(), cancellationToken);
            await SaveConversationAsync(request.Text, reply ?? string.Empty, cancellationToken);

            if (!string.IsNullOrWhiteSpace(reply) && reply.Length > _maxOutputChars)
            {
                reply = reply[.._maxOutputChars] + "...";
            }

            return new JsonResult(new { reply = reply ?? "Yanıt üretilemedi." });
        }

        public class QaRequest
        {
            public string Text { get; set; } = string.Empty;
        }

        private async Task SaveConversationAsync(string input, string reply, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(reply))
            {
                return;
            }

            _db.DemoChatServices!.Add(new DemoChatService
            {
                Input = input.Trim(),
                Reply = reply.Trim(),
                SessionId = HttpContext.Session.Id,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}