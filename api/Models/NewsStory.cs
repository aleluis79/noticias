namespace NewsAggregator.Models
{
    /// <summary>
    /// One outlet's version of a story. A story covered by several outlets
    /// keeps every version so the reader can pick which one to read.
    /// </summary>
    public class NewsStorySource
    {
        public string SourceName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;

        /// <summary>
        /// Null when the outlet's home page did not expose a real publication
        /// date. It is never filled with the retrieval time.
        /// </summary>
        public DateTime? PublishedDate { get; set; }
    }

    /// <summary>
    /// A group of articles that report the same story, plus the version shown
    /// on the front page.
    /// </summary>
    public class NewsStory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Headline of the leading outlet, verbatim.</summary>
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string OriginalUrl { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;

        /// <summary>Number of distinct outlets covering the story.</summary>
        public int SourceCount { get; set; }

        /// <summary>Number of articles, which can exceed <see cref="SourceCount"/>.</summary>
        public int ArticleCount { get; set; }

        /// <summary>Most recent real publication date in the group, if any.</summary>
        public DateTime? LatestPublishedDate { get; set; }

        public IReadOnlyList<NewsStorySource> Sources { get; set; } = Array.Empty<NewsStorySource>();
    }
}
