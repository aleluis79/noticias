using NewsAggregator.Models;

namespace NewsAggregator.Services
{
    public interface INewsAggregationService
    {
        /// <summary>
        /// Returns the aggregated news. Results are cached for a few minutes unless
        /// <paramref name="forceRefresh"/> is true.
        /// </summary>
        Task<IEnumerable<NewsArticle>> GetNewsAsync(bool forceRefresh = false);
    }
}
