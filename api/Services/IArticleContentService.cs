using NewsAggregator.Models;

namespace NewsAggregator.Services
{
    public interface IArticleContentService
    {
        /// <summary>True when the URL belongs to one of the supported news sources.</summary>
        bool IsSupportedUrl(string? url);

        /// <summary>
        /// Downloads the article page and extracts its body. Returns null when the page
        /// cannot be fetched or no meaningful text is found.
        /// </summary>
        Task<ArticleContent?> GetArticleContentAsync(string url, CancellationToken cancellationToken = default);
    }
}
