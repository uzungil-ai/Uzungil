namespace Roma.Services
{
    public interface IPaymentService
    {
        /// <summary>
        /// Create a hosted checkout URL for the given destination/image and amount.
        /// Return null if no external checkout is configured (caller should fallback to demo flow).
        /// </summary>
        Task<string?> CreateCheckoutSessionUrlAsync(int destinationId, int imageId, decimal amount, string successUrl, string cancelUrl);
    }
}
