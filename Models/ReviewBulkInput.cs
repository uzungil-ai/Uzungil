namespace Roma.Models
{
    public class ReviewBulkInput
    {
        public int AttractionId { get; set; }
        public bool IsSelected { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int Rating { get; set; } = 5;
    }
}
