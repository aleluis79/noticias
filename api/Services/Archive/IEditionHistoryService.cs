using NewsAggregator.Models;

namespace NewsAggregator.Services.Archive
{
    /// <summary>
    /// Reads stored editions, groups them into stories and keeps the grouped
    /// result in memory because past editions are immutable.
    /// </summary>
    public interface IEditionHistoryService
    {
        /// <summary>Number of previous days that the archive can offer.</summary>
        int PreviousDaysAvailable { get; }

        Task<IReadOnlyList<NewsDaySummary>> GetPreviousDaySummariesAsync(
            DateOnly today,
            CancellationToken cancellationToken = default);

        /// <summary>Returns a stored edition, or null when that date is missing.</summary>
        Task<StoriesResponse?> GetEditionAsync(
            DateOnly date,
            CancellationToken cancellationToken = default);
    }
}
