using NewsAggregator.Models;

namespace NewsAggregator.Services.Parsers
{
    public interface IParser
    {
        /// <summary>Human readable name of the news outlet.</summary>
        string SourceName { get; }

        /// <summary>Home page that this parser knows how to read.</summary>
        string BaseUrl { get; }

        /// <summary>Extracts the most relevant articles found in the given HTML.</summary>
        IReadOnlyList<NewsArticle> Parse(string rawHtml);
    }
}
