namespace Roma.Services
{
    /// <summary>
    /// Demo/no-op payment service for local development. Returns null so app falls back to built-in demo flow.
    /// Replace with a real implementation (Stripe/PayPal) in production.
    /// </summary>
    public class DemoPaymentService : IPaymentService
    {
        public Task<string?> CreateCheckoutSessionUrlAsync(int destinationId, int imageId, decimal amount, string successUrl, string cancelUrl)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
