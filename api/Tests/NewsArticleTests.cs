using Xunit;
using NewsAggregator.Models;

public class NewsArticleTests
{
    [Fact]
    public void NewsArticle_ShouldHaveDefaultValues()
    {
        // Arrange
        var article = new NewsArticle();

        // Act
        // Check defaults
        var id = article.Id;

        // Assert
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(string.Empty, article.Title);
        Assert.Equal(string.Empty, article.Summary);
        Assert.Equal(string.Empty, article.OriginalUrl);
        Assert.Equal(string.Empty, article.SourceName);
        Assert.Equal(DateTimeKind.Utc, article.PublishedDate.Kind);
        Assert.True((DateTime.UtcNow - article.PublishedDate).Duration() < TimeSpan.FromMinutes(1));
    }
}