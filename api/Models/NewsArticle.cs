namespace NewsAggregator.Models
{
    public class NewsArticle
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;

        /// <summary>
        /// Publication date as published by the outlet, when the home page
        /// exposes it. Null means unknown: it is never filled with the
        /// retrieval time, because the UI must not present a crawl time as a
        /// publication time.
        /// </summary>
        public DateTime? PublishedDate { get; set; }
    }
}
