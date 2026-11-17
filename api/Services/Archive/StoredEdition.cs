using NewsAggregator.Models;

namespace NewsAggregator.Services.Archive
{
    /// <summary>One persisted edition: the articles fetched for a given date.</summary>
    public sealed class StoredEdition
    {
        public DateOnly Date { get; set; }

        /// <summary>Moment when this snapshot was captured, in UTC.</summary>
        public DateTime CapturedAt { get; set; }

        public IReadOnlyList<NewsArticle> Articles { get; set; } = Array.Empty<NewsArticle>();
    }
}
