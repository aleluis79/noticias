using NewsAggregator.Models;

namespace NewsAggregator.Services.Archive
{
    public interface IDailyEditionStore
    {
        /// <summary>
        /// Stores the edition for <paramref name="date"/>. An empty article list
        /// is ignored so a failed aggregation never overwrites a valid edition.
        /// </summary>
        Task SaveAsync(
            DateOnly date,
            IReadOnlyList<NewsArticle> articles,
            DateTime capturedAtUtc,
            CancellationToken cancellationToken = default);

        /// <summary>Returns the stored edition for a date, or null when absent.</summary>
        Task<StoredEdition?> GetAsync(DateOnly date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored editions before <paramref name="today"/>, newest
        /// first, limited to <paramref name="count"/>.
        /// </summary>
        Task<IReadOnlyList<StoredEdition>> GetPreviousAsync(
            DateOnly today,
            int count,
            CancellationToken cancellationToken = default);
    }
}
