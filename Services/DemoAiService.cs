namespace Roma.Services
{
    public class DemoAiService : IAiService
    {
        public Task<string> GenerateDescriptionAsync(string prompt)
        {
            // simple deterministic demo: echo with summary
            var shortDesc = prompt.Length > 200 ? prompt.Substring(0, 200) + "..." : prompt;
            var result = $"(AI-generated) {shortDesc}";
            return Task.FromResult(result);
        }
    }
}
