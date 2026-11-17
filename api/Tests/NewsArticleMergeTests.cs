using NewsAggregator.Models;
using NewsAggregator.Services;
using Xunit;

public class NewsArticleMergeTests
{
    [Fact]
    public void MergeSourceArticles_ShouldKeepHomeFirstAndIgnoreQueryString()
    {
        var home = new[]
        {
            Article("Portada uno", "https://www.example.com/politica/portada-uno"),
            Article("Portada dos", "https://www.example.com/economia/portada-dos")
        };

        var sections = new[]
        {
            new[]
            {
                Article("Portada uno repetida", "https://www.example.com/politica/portada-uno?utm=seccion"),
                Article("Sección uno", "https://www.example.com/sociedad/seccion-uno")
            }
        };

        var merged = NewsAggregationService.MergeSourceArticles(home, sections);

        Assert.Equal(
            new[] { "Portada uno", "Portada dos", "Sección uno" },
            merged.Select(article => article.Title));
    }

    [Fact]
    public void MergeSourceArticles_ShouldDropRepeatedTitlesAndTrailingSlashes()
    {
        var home = new[]
        {
            Article("El dólar cerró en alza", "https://www.example.com/economia/dolar-hoy/")
        };

        var sections = new[]
        {
            new[]
            {
                Article("El dólar cerró en alza", "https://www.example.com/economia/otra-url"),
                Article("Otra nota distinta", "https://www.example.com/economia/dolar-hoy")
            }
        };

        var merged = NewsAggregationService.MergeSourceArticles(home, sections);

        var article = Assert.Single(merged);
        Assert.Equal("El dólar cerró en alza", article.Title);
    }

    [Fact]
    public void MergeSourceArticles_ShouldCapAtFiftyArticles()
    {
        var home = Enumerable.Range(1, 30)
            .Select(index => Article($"Portada {index}", $"https://www.example.com/politica/portada-{index}"))
            .ToArray();

        var sections = Enumerable.Range(1, 3)
            .Select(section => Enumerable.Range(1, 12)
                .Select(index => Article(
                    $"Sección {section} nota {index}",
                    $"https://www.example.com/seccion-{section}/nota-{index}"))
                .ToArray())
            .ToArray();

        var merged = NewsAggregationService.MergeSourceArticles(home, sections);

        Assert.Equal(NewsAggregationService.MaxArticlesPerSource, merged.Count);
        Assert.Equal("Portada 1", merged[0].Title);
        Assert.Equal("Portada 30", merged[29].Title);
        Assert.Equal("Sección 1 nota 1", merged[30].Title);
    }

    private static NewsArticle Article(string title, string url) => new()
    {
        Title = title,
        OriginalUrl = url,
        SourceName = "Test News"
    };
}
