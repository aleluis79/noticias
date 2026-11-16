namespace NewsAggregator.Models
{
    /// <summary>
    /// Full body of a news article, extracted on demand from its original URL.
    /// </summary>
    public class ArticleContent
    {
        public string Url { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        /// <summary>Author byline published by the source, when available.</summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>Article body split into plain-text paragraphs.</summary>
        public IReadOnlyList<string> Paragraphs { get; set; } = Array.Empty<string>();
    }
}
