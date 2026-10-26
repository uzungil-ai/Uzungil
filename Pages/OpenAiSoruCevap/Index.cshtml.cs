using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roma.Services;

namespace Roma.Pages.OpenAiSoruCevap
{
    public class IndexModel : PageModel
    {
        private readonly IOpenAiQaService _qaService;
        private readonly int _maxInputChars;
        private readonly int _maxOutputChars;

        public IndexModel(IOpenAiQaService qaService, IConfiguration configuration)
        {
            _qaService = qaService;
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
    }
}