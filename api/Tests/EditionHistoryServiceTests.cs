using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NewsAggregator.Models;
using NewsAggregator.Services.Archive;
using NewsAggregator.Services.Grouping;
using Xunit;

public class EditionHistoryServiceTests
{
    private readonly Mock<IDailyEditionStore> _store = new();
    private readonly Mock<IStoryGroupingService> _grouping = new();

    public EditionHistoryServiceTests()
    {
        _grouping
            .Setup(service => service.Group(It.IsAny<IReadOnlyList<NewsArticle>>()))
            .Returns((IReadOnlyList<NewsArticle> articles) => new[]
            {
                new NewsStory
                {
                    Title = articles[0].Title,
                    SourceName = articles[0].SourceName,
                    OriginalUrl = articles[0].OriginalUrl,
                    SourceCount = articles.Select(article => article.SourceName).Distinct().Count(),
                    ArticleCount = articles.Count,
                    Sources = articles.Select(article => new NewsStorySource
                    {
                        SourceName = article.SourceName,
                        Title = article.Title,
                        OriginalUrl = article.OriginalUrl
                    }).ToArray()
                }
            });
    }

    [Fact]
    public async Task GetEditionAsync_ShouldReturnTheStoredEditionGrouped()
    {
        var date = new DateOnly(2026, 11, 16);
        _store
            .Setup(store => store.GetAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Edition(date, "Ayer"));

        var service = CreateService();

        var response = await service.GetEditionAsync(date);

        Assert.NotNull(response);
        Assert.Equal("2026-11-16", response!.Date);
        Assert.Equal(2, response.ArticleCount);
        Assert.Equal(1, response.StoryCount);
        Assert.Single(response.Stories);
        Assert.Empty(response.PreviousDays);
    }

    [Fact]
    public async Task GetPreviousDaySummariesAsync_ShouldReturnNewestFirstAndSkipMissingDays()
    {
        var today = new DateOnly(2026, 11, 17);
        _store
            .Setup(store => store.GetAsync(new DateOnly(2026, 11, 16), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Edition(new DateOnly(2026, 11, 16), "Ayer"));
        _store
            .Setup(store => store.GetAsync(new DateOnly(2026, 11, 15), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoredEdition?)null);

        var service = CreateService();

        var summaries = await service.GetPreviousDaySummariesAsync(today);

        var summary = Assert.Single(summaries);
        Assert.Equal("2026-11-16", summary.Date);
        Assert.Equal(2, summary.ArticleCount);
        Assert.Equal(1, summary.StoryCount);
    }

    [Fact]
    public async Task GetEditionAsync_ShouldReuseTheGroupedEditionFromMemory()
    {
        var date = new DateOnly(2026, 11, 16);
        _store
            .Setup(store => store.GetAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Edition(date, "Ayer"));

        var service = CreateService();

        await service.GetEditionAsync(date);
        await service.GetEditionAsync(date);

        _store.Verify(store => store.GetAsync(date, It.IsAny<CancellationToken>()), Times.Once);
        _grouping.Verify(
            grouping => grouping.Group(It.IsAny<IReadOnlyList<NewsArticle>>()),
            Times.Once);
    }

    private EditionHistoryService CreateService() =>
        new(
            _store.Object,
            _grouping.Object,
            Options.Create(new ArchiveOptions { RetentionDays = 3 }),
            NullLogger<EditionHistoryService>.Instance);

    private static StoredEdition Edition(DateOnly date, string titlePrefix) => new()
    {
        Date = date,
        CapturedAt = new DateTime(2026, 11, 16, 23, 0, 0, DateTimeKind.Utc),
        Articles = new[]
        {
            new NewsArticle
            {
                Title = $"{titlePrefix} uno",
                OriginalUrl = "https://www.example.com/uno",
                SourceName = "Clarín"
            },
            new NewsArticle
            {
                Title = $"{titlePrefix} dos",
                OriginalUrl = "https://www.example.com/dos",
                SourceName = "La Nación"
            }
        }
    };
}
