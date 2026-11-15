namespace NewsAggregator.Models
{
    public class NewsArticle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;
    }
}