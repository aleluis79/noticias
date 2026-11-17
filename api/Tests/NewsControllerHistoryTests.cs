using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.Services.Archive;
using NewsAggregator.Services.Grouping;
using NewsAggregator.Services.Tts;
using Xunit;

public class NewsControllerHistoryTests
{
    private static readonly DateOnly Today = new(2026, 11, 17);

    private readonly Mock<INewsAggregationService> _news = new();
    private readonly Mock<IStoryGroupingService> _grouping = new();
    private readonly Mock<IEditionHistoryService> _history = new();
    private readonly FixedTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero));

    public NewsControllerHistoryTests()
    {
        _news
            .Setup(service => service.GetNewsAsync(It.IsAny<bool>()))
            .ReturnsAsync(new[]
            {
                new NewsArticle
                {
                    Title = "Una noticia de hoy",
                    OriginalUrl = "https://www.example.com/hoy",
                    SourceName = "Clarín"
                }
            });
        _grouping
            .Setup(service => service.Group(It.IsAny<IReadOnlyList<NewsArticle>>()))
            .Returns(new[] { Story("Una noticia de hoy") });
        _history.SetupGet(service => service.PreviousDaysAvailable).Returns(2);
        _history
            .Setup(service => service.GetPreviousDaySummariesAsync(Today, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new NewsDaySummary { Date = "2026-11-16", ArticleCount = 20, StoryCount = 18 },
                new NewsDaySummary { Date = "2026-11-15", ArticleCount = 19, StoryCount = 17 }
            });
    }

    [Fact]
    public async Task GetStories_ShouldReturnTodayWithPreviousDaySummaries()
    {
        var controller = CreateController();

        var result = await controller.GetStories(date: null, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<StoriesResponse>(ok.Value);
        Assert.Equal("2026-11-17", response.Date);
        Assert.Equal(1, response.ArticleCount);
        Assert.Equal(1, response.StoryCount);
        Assert.Equal(2, response.PreviousDays.Count);
        Assert.Equal("2026-11-16", response.PreviousDays[0].Date);
    }

    [Fact]
    public async Task GetStories_WithStoredDate_ReturnsTheArchiveWithoutFetching()
    {
        var date = new DateOnly(2026, 11, 16);
        _history
            .Setup(service => service.GetEditionAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoriesResponse
            {
                Date = "2026-11-16",
                ArticleCount = 20,
                StoryCount = 18,
                Stories = new[] { Story("Una noticia de ayer") }
            });

        var controller = CreateController();

        var result = await controller.GetStories(date: "2026-11-16", cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<StoriesResponse>(ok.Value);
        Assert.Equal("2026-11-16", response.Date);
        _news.Verify(service => service.GetNewsAsync(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GetStories_WithMissingStoredDate_ReturnsNotFound()
    {
        var date = new DateOnly(2026, 11, 16);
        _history
            .Setup(service => service.GetEditionAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StoriesResponse?)null);

        var controller = CreateController();

        var result = await controller.GetStories(date: "2026-11-16", cancellationToken: CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("16-11-2026")]
    [InlineData("no-es-una-fecha")]
    [InlineData("2026-11-18")]
    [InlineData("2026-11-14")]
    public async Task GetStories_WithInvalidOrOutOfWindowDate_ReturnsBadRequest(string date)
    {
        var controller = CreateController();

        var result = await controller.GetStories(date: date, cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        _history.Verify(
            service => service.GetEditionAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetStories_WithTodayAsDate_ReturnsToday()
    {
        var controller = CreateController();

        var result = await controller.GetStories(date: "2026-11-17", cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<StoriesResponse>(ok.Value);
        Assert.Equal("2026-11-17", response.Date);
        _news.Verify(service => service.GetNewsAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task GetNews_ShouldKeepTheOriginalResponseShape()
    {
        var controller = CreateController();

        var result = await controller.GetNews();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var articles = Assert.IsAssignableFrom<IEnumerable<NewsArticle>>(ok.Value);
        var article = Assert.Single(articles);
        Assert.Equal("Una noticia de hoy", article.Title);
    }

    private NewsController CreateController() =>
        new(
            _news.Object,
            Mock.Of<IArticleContentService>(),
            _grouping.Object,
            Mock.Of<ITextToSpeechService>(),
            _history.Object,
            new EditionClock(_timeProvider, Options.Create(new ArchiveOptions())))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

    private static NewsStory Story(string title) => new()
    {
        Title = title,
        SourceName = "Clarín",
        OriginalUrl = "https://www.example.com/nota",
        SourceCount = 1,
        ArticleCount = 1,
        Sources = new[]
        {
            new NewsStorySource
            {
                SourceName = "Clarín",
                Title = title,
                OriginalUrl = "https://www.example.com/nota"
            }
        }
    };
}
