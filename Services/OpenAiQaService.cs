using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Roma.Services
{
    public class OpenAiQaService : IOpenAiQaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAiQaService> _logger;

        public OpenAiQaService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OpenAiQaService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
        {
            // Environment variable should have priority to avoid stale JSON keys.
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? _configuration["OpenAI:ApiKey"];

            apiKey = apiKey?.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "OpenAI API anahtarı bulunamadı.";
            }

            var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
            var maxTokens = _configuration.GetValue<int?>("OpenAI:MaxOutputTokens") ?? 300;
            const string endpoint = "https://api.openai.com/v1/responses";

            try
            {
                using var client = _httpClientFactory.CreateClient("OpenAI");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model,
                max_output_tokens = maxTokens,
                temperature = 0.1,
                input = new object[]
                {
                    new
                    {
                        role = "system",
                        content = new object[]
                        {
                            new { type = "input_text", text = "Sen güvenilir bir asistansın. Türkçe, net ve kısa yanıt ver. Bilmediğin durumda tahmin etme, 'emin değilim' de." }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = question }
                        }
                    }
                }
            };

                using var response = await client.PostAsync(
                    endpoint,
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                    cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errorMessage = TryReadErrorMessage(body);
                    _logger.LogWarning("OpenAI isteği başarısız. StatusCode: {StatusCode}, Error: {Error}", (int)response.StatusCode, errorMessage);
                    return $"OpenAI isteği başarısız oldu ({(int)response.StatusCode}). {errorMessage}";
                }

                using var doc = JsonDocument.Parse(body);
                var answer = ExtractAnswer(doc.RootElement);
                if (!string.IsNullOrWhiteSpace(answer))
                {
                    return answer;
                }

                _logger.LogWarning("OpenAI yanıtı parse edilemedi. Body: {Body}", Truncate(body, 500));
                return $"Yanıt üretilemedi. Ham yanıt: {Truncate(body, 280)}";
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "OpenAI isteği zaman aşımına uğradı.");
                return "OpenAI isteği zaman aşımına uğradı. Lütfen tekrar deneyin.";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "OpenAI API bağlantı hatası.");
                return $"OpenAI API bağlantı hatası: {ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI API hatası oluştu.");
                return $"OpenAI API hatası oluştu: {ex.Message}";
            }
        }

        private static string? ExtractAnswer(JsonElement root)
        {
            if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
            {
                var text = outputText.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            if (root.TryGetProperty("output", out var outputArr) && outputArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var outputItem in outputArr.EnumerateArray())
                {
                    if (!outputItem.TryGetProperty("content", out var contentArr) || contentArr.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var contentItem in contentArr.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                        {
                            var text = textProp.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                return text.Trim();
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                foreach (var choice in choices.EnumerateArray())
                {
                    if (choice.TryGetProperty("message", out var message)
                        && message.TryGetProperty("content", out var content)
                        && content.ValueKind == JsonValueKind.String)
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Trim();
                        }
                    }
                }
            }

            return null;
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
            {
                return value;
            }

            return value[..max] + "...";
        }

        private static string TryReadErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "Boş hata yanıtı.";
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var errorObj))
                {
                    if (errorObj.TryGetProperty("message", out var messageProp))
                    {
                        return messageProp.GetString() ?? "Bilinmeyen hata.";
                    }
                }
            }
            catch
            {
                // ignore parse errors and return raw body below
            }

            return body.Length > 300 ? body[..300] + "..." : body;
        }
    }
}