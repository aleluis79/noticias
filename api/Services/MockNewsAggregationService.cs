using NewsAggregator.Models;
using NewsAggregator.Services;

namespace NewsAggregator.Services
{
    public class MockNewsAggregationService : INewsAggregationService
    {
        public Task<IEnumerable<NewsArticle>> GetNewsAsync(bool forceRefresh = false)
        {
            // This mock is used until real fetching (Tasks 6, 7, 8) is implemented.
            var mockArticles = new List<NewsArticle>
            {
                new NewsArticle { Title = "Mock News 1", Summary = "Mock summary 1.", OriginalUrl = "http://mock.com/1", SourceName = "MockSource" },
                new NewsArticle { Title = "Mock News 2", Summary = "Mock summary 2.", OriginalUrl = "http://mock.com/2", SourceName = "MockSource" }
            };
            return Task.FromResult<IEnumerable<NewsArticle>>(mockArticles);
        }
    }
}
