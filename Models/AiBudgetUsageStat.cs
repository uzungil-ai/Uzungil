namespace Roma.Models
{
    public class AiBudgetUsageStat
    {
        public int Id { get; set; }
        public int MonthKey { get; set; }
        public int EstimatedRequestCount { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
