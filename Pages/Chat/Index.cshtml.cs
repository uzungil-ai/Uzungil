using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roma.Data;
using Roma.Models;
using Roma.Services;

namespace Roma.Pages.Chat
{
    public class IndexModel : PageModel
    {
        private readonly IChatService _chat;
        private readonly ApplicationDbContext _db;
        private readonly decimal _monthlyBudgetUsd;
        private readonly decimal _estimatedCostPerRequestUsd;
        private readonly int _maxRequestsPerMinutePerSession;
        private readonly int _maxInputChars;
        private readonly int _maxOutputChars;

        public List<ChatHistoryMessage> HistoryMessages { get; private set; } = new();

        public IndexModel(IChatService chat, ApplicationDbContext db, IConfiguration configuration)
        {
            _chat = chat;
            _db = db;
            _monthlyBudgetUsd = configuration.GetValue<decimal?>("AiBudget:MonthlyBudgetUsd") ?? 10m;
            _estimatedCostPerRequestUsd = configuration.GetValue<decimal?>("AiBudget:EstimatedCostPerRequestUsd") ?? 0.002m;
            _maxRequestsPerMinutePerSession = configuration.GetValue<int?>("AiBudget:MaxRequestsPerMinutePerSession") ?? 8;
            _maxInputChars = configuration.GetValue<int?>("AiBudget:MaxInputChars") ?? 500;
            _maxOutputChars = configuration.GetValue<int?>("AiBudget:MaxOutputChars") ?? 400;
        }

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var sessionId = HttpContext.Session.Id;
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var recentPairs = await _db.DemoChatServices!
                .AsNoTracking()
                .Where(x => x.SessionId == sessionId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(30)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new { x.Input, x.Reply })
                .ToListAsync(cancellationToken);

            foreach (var pair in recentPairs)
            {
                if (!string.IsNullOrWhiteSpace(pair.Input))
                {
                    HistoryMessages.Add(new ChatHistoryMessage { Role = "user", Text = pair.Input! });
                }

                if (!string.IsNullOrWhiteSpace(pair.Reply))
                {
                    HistoryMessages.Add(new ChatHistoryMessage { Role = "bot", Text = pair.Reply! });
                }
            }
        }

        public async Task<IActionResult> OnPostSendAsync([FromBody] ChatRequest req, CancellationToken cancellationToken)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Text)) return BadRequest();

            if (req.Text.Length > _maxInputChars)
            {
                return new JsonResult(new { reply = $"Mesajınız çok uzun. Lütfen en fazla {_maxInputChars} karakter gönderin." });
            }

            if (!AllowRequestInRateWindow())
            {
                return new JsonResult(new { reply = "Çok hızlı mesaj gönderiyorsunuz. Lütfen kısa bir süre bekleyip tekrar deneyin." });
            }

            if (!await TryReserveEstimatedBudgetAsync(cancellationToken))
            {
                return new JsonResult(new { reply = "Bu ay için yapay zeka kullanım limiti doldu. Lütfen daha sonra tekrar deneyin." });
            }

            var normalizedInput = req.Text.Trim().ToLowerInvariant();
            var isAwaitingSubscribeAnswer = HttpContext.Session.GetString("ChatSubscribePromptPending") == "1";
            var isAffirmative = normalizedInput == "evet" || normalizedInput == "olur" || normalizedInput == "tabii" || normalizedInput == "tamam" || normalizedInput.Contains("evet");
            var hasMoreQuestions = normalizedInput == "var" || normalizedInput.Contains("var") || normalizedInput.Contains("sorum var");
            var noMoreQuestions = normalizedInput == "yok" || normalizedInput.Contains("başka sorum yok") || normalizedInput.Contains("sorum yok") || normalizedInput.Contains("hayır");

            if (isAwaitingSubscribeAnswer && isAffirmative)
            {
                HttpContext.Session.Remove("ChatSubscribePromptPending");
                return new JsonResult(new
                {
                    reply = "Harika! E-posta kayıt sayfasına yönlendiriyorum.",
                    redirectUrl = "/Subscribe"
                });
            }

            if (isAwaitingSubscribeAnswer && hasMoreQuestions)
            {
                HttpContext.Session.Remove("ChatSubscribePromptPending");
                return new JsonResult(new { reply = "Lütfen yazınız." });
            }

            if (isAwaitingSubscribeAnswer && noMoreQuestions)
            {
                HttpContext.Session.Remove("ChatSubscribePromptPending");
                return new JsonResult(new { reply = "Tamamdır. İsterseniz daha sonra tekrar sorabilirsiniz." });
            }

            if (isAwaitingSubscribeAnswer)
            {
                // Kullanıcı yönlendirme sorusunu atlayıp yeni soru yazdıysa akışı normale döndür.
                HttpContext.Session.Remove("ChatSubscribePromptPending");
            }

            var reply = await _chat.GetReplyAsync(req.Text, HttpContext.Session.Id, cancellationToken);
            if (!string.IsNullOrEmpty(reply) && reply.Length > _maxOutputChars)
            {
                reply = reply[.._maxOutputChars] + "...";
            }

            await SaveConversationAsync(req.Text, reply, cancellationToken);

            var questionCount = HttpContext.Session.GetInt32("ChatQuestionCount") ?? 0;
            questionCount++;
            HttpContext.Session.SetInt32("ChatQuestionCount", questionCount);

            var postFivePromptShown = HttpContext.Session.GetString("ChatPostFivePromptShown") == "1";
            if (!postFivePromptShown && questionCount >= 5)
            {
                reply = $"{reply} Başka soru yoksa e-posta ile eklenen destinasyonlardan haberdar olmak ister misiniz? Başka sorunuz var mı?";
                HttpContext.Session.SetString("ChatSubscribePromptPending", "1");
                HttpContext.Session.SetString("ChatPostFivePromptShown", "1");
            }

            return new JsonResult(new { reply });
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

        public class ChatHistoryMessage
        {
            public string Role { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        public class ChatRequest { public string Text { get; set; } = string.Empty; }

        private bool AllowRequestInRateWindow()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var windowStart = long.TryParse(HttpContext.Session.GetString("ChatRateWindowStart"), out var parsedStart)
                ? parsedStart
                : now;

            var count = HttpContext.Session.GetInt32("ChatRateCount") ?? 0;
            if (now - windowStart >= 60)
            {
                windowStart = now;
                count = 0;
            }

            if (count >= _maxRequestsPerMinutePerSession)
            {
                HttpContext.Session.SetString("ChatRateWindowStart", windowStart.ToString());
                HttpContext.Session.SetInt32("ChatRateCount", count);
                return false;
            }

            count++;
            HttpContext.Session.SetString("ChatRateWindowStart", windowStart.ToString());
            HttpContext.Session.SetInt32("ChatRateCount", count);
            return true;
        }

        private async Task<bool> TryReserveEstimatedBudgetAsync(CancellationToken cancellationToken)
        {
            if (_monthlyBudgetUsd <= 0 || _estimatedCostPerRequestUsd <= 0)
            {
                return true;
            }

            var monthKey = (DateTime.UtcNow.Year * 100) + DateTime.UtcNow.Month;
            var usage = await _db.AiBudgetUsageStats!
                .SingleOrDefaultAsync(x => x.MonthKey == monthKey, cancellationToken);

            if (usage == null)
            {
                usage = new AiBudgetUsageStat
                {
                    MonthKey = monthKey,
                    EstimatedRequestCount = 0,
                    EstimatedCostUsd = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.AiBudgetUsageStats!.Add(usage);
            }

            var nextCount = usage.EstimatedRequestCount + 1;
            var nextEstimatedCost = nextCount * _estimatedCostPerRequestUsd;
            if (nextEstimatedCost > _monthlyBudgetUsd)
            {
                return false;
            }

            usage.EstimatedRequestCount = nextCount;
            usage.EstimatedCostUsd = nextEstimatedCost;
            usage.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
