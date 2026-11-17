using NewsAggregator.Models;

namespace NewsAggregator.Services.Parsers
{
    public interface IParser
    {
        /// <summary>Human readable name of the news outlet.</summary>
        string SourceName { get; }

        /// <summary>Home page that this parser knows how to read.</summary>
        string BaseUrl { get; }

        /// <summary>
        /// Section pages that complement the home page for this outlet.
        /// Empty when the outlet has no useful sections.
        /// </summary>
        IReadOnlyList<string> SectionUrls { get; }

        /// <summary>
        /// Extracts the most relevant articles found in the given HTML. When
        /// <paramref name="maxArticles"/> is omitted the parser uses its own limit.
        /// </summary>
        IReadOnlyList<NewsArticle> Parse(string rawHtml, int? maxArticles = null);
    }
}
