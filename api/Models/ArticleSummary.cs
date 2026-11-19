namespace NewsAggregator.Models
{
    /// <summary>
    /// Short summary of a news article, generated on demand and kept on disk so
    /// it is never generated twice.
    /// </summary>
    public class ArticleSummary
    {
        public string Url { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        /// <summary>Model that produced the summary when it was generated.</summary>
        public string Model { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; }
    }
}
