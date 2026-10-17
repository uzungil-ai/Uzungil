namespace Roma.Services
{
    public class AiJob
    {
        public int DestinationId { get; set; }
        public int ImageId { get; set; }
        public string JobType { get; set; } = "AltText"; // or Description, etc.
        public string? Payload { get; set; }
    }
}
