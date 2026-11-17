namespace NewsAggregator.Models
{
    /// <summary>Response for the front page and for a stored edition.</summary>
    public class StoriesResponse
    {
        /// <summary>Edition date in Argentina time, as YYYY-MM-DD.</summary>
        public string Date { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; }
        public int ArticleCount { get; set; }
        public int StoryCount { get; set; }
        public IReadOnlyList<NewsStory> Stories { get; set; } = Array.Empty<NewsStory>();

        /// <summary>
        /// Stored days before this edition, newest first. Empty for a
        /// historical edition.
        /// </summary>
        public IReadOnlyList<NewsDaySummary> PreviousDays { get; set; } = Array.Empty<NewsDaySummary>();
    }

    /// <summary>Lightweight description of a stored previous day.</summary>
    public class NewsDaySummary
    {
        public string Date { get; set; } = string.Empty;
        public int ArticleCount { get; set; }
        public int StoryCount { get; set; }
    }
}
