namespace Roma.Models
{
    public class SubscriberDashboardViewModel
    {
        public List<Subscriber> PendingSubscribers { get; set; } = new();
        public List<Subscriber> ConfirmedSubscribers { get; set; } = new();
        public List<Subscriber> CancelledSubscribers { get; set; } = new();
        public List<SentEmail> SentMessages { get; set; } = new();
        public List<Review> PendingReviews { get; set; } = new();
        public List<Review> ApprovedReviews { get; set; } = new();
        public List<Review> RejectedReviews { get; set; } = new();
    }
}
