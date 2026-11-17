using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.Services.Archive;
using Xunit;

public class NewsAggregationServiceTests
{
    private const int Sources = 13;
    private const int SectionPages = 45;
    private const int Pages = Sources + SectionPages;
    private const int ArticlesPerPage = 2;

    private readonly Mock<IPlaywright> _mockPlaywright = new();
    private readonly Mock<IBrowserType> _mockBrowserType = new();
    private readonly Mock<IBrowser> _mockBrowser = new();
    private readonly Mock<IBrowserContext> _mockContext = new();
    private readonly Mock<IPage> _mockPage = new();
    private readonly Mock<IResponse> _mockResponse = new();
    private readonly Mock<IDailyEditionStore> _store = new();
    private readonly FixedTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 11, 17, 15, 0, 0, TimeSpan.Zero));

    public NewsAggregationServiceTests()
    {
        _store
            .Setup(store => store.SaveAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<IReadOnlyList<NewsArticle>>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPlaywright.Setup(p => p.Chromium).Returns(_mockBrowserType.Object);
        _mockBrowserType
            .Setup(bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()))
            .ReturnsAsync(_mockBrowser.Object);
        _mockBrowser
            .Setup(b => b.NewContextAsync(It.IsAny<BrowserNewContextOptions>()))
            .ReturnsAsync(_mockContext.Object);
        _mockBrowser
            .Setup(b => b.CloseAsync(It.IsAny<BrowserCloseOptions>()))
            .Returns(Task.CompletedTask);
        _mockContext
            .Setup(c => c.NewPageAsync())
            .ReturnsAsync(_mockPage.Object);
        _mockContext
            .Setup(c => c.RouteAsync(It.IsAny<string>(), It.IsAny<Func<IRoute, Task>>(), It.IsAny<BrowserContextRouteOptions>()))
            .Returns(Task.CompletedTask);
        _mockPage
            .Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions>()))
            .ReturnsAsync(_mockResponse.Object);
        _mockPage
            .Setup(p => p.WaitForLoadStateAsync(It.IsAny<LoadState>(), It.IsAny<PageWaitForLoadStateOptions>()))
            .Returns(Task.CompletedTask);
        _mockPage
            .Setup(p => p.ContentAsync())
            .ReturnsAsync(() => BuildPageHtml(new Uri("https://fallback.example.com/")));
        _mockPage.Setup(p => p.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(Task.CompletedTask);
        _mockResponse.Setup(r => r.Ok).Returns(true);
    }

    [Fact]
    public async Task GetNewsAsync_ShouldParseHomeAndSectionPagesOverHttp()
    {
        // Arrange
        var handler = new FakeHttpHandler(request => Ok(BuildPageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            // Act
            var result = (await service.GetNewsAsync()).ToList();

            // Assert
            Assert.Equal(Pages * ArticlesPerPage, result.Count);
            Assert.Equal(Pages, handler.RequestCount);
            Assert.All(result, article => Assert.StartsWith("https://", article.OriginalUrl));
            Assert.All(result, article => Assert.False(string.IsNullOrWhiteSpace(article.Title)));
            Assert.All(result, article => Assert.False(string.IsNullOrWhiteSpace(article.SourceName)));

            // Static HTML was enough, so the browser must not be launched.
            _mockBrowserType.Verify(
                bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()),
                Times.Never);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldStoreTheEditionAfterAFreshFetch()
    {
        // Arrange
        var handler = new FakeHttpHandler(request => Ok(BuildPageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            // Act
            await service.GetNewsAsync();
            await service.GetNewsAsync();

            // Assert
            _store.Verify(
                store => store.SaveAsync(
                    new DateOnly(2026, 11, 17),
                    It.IsAny<IReadOnlyList<NewsArticle>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFetchAgainWhenTheArgentinaDateChanges()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 18, 2, 59, 0, TimeSpan.Zero));
        var handler = new FakeHttpHandler(request => Ok(BuildPageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            // Act: 23:59 in Argentina.
            await service.GetNewsAsync();
            Assert.Equal(Pages, handler.RequestCount);

            // Act: 00:01 in Argentina, the cached edition belongs to the previous day.
            _timeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 18, 3, 1, 0, TimeSpan.Zero));
            await service.GetNewsAsync();

            // Assert
            Assert.Equal(Pages * 2, handler.RequestCount);
            _store.Verify(
                store => store.SaveAsync(
                    new DateOnly(2026, 11, 17),
                    It.IsAny<IReadOnlyList<NewsArticle>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            _store.Verify(
                store => store.SaveAsync(
                    new DateOnly(2026, 11, 18),
                    It.IsAny<IReadOnlyList<NewsArticle>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldNotServeThePreviousDaySnapshotAfterMidnight()
    {
        // Arrange
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 18, 2, 59, 0, TimeSpan.Zero));
        var handler = new FakeHttpHandler(request => Ok(BuildPageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            await service.GetNewsAsync();

            // The new day fails completely, including the browser fallback.
            _timeProvider.SetUtcNow(new DateTimeOffset(2026, 11, 18, 3, 1, 0, TimeSpan.Zero));
            handler.SetHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            _mockPage.Setup(page => page.ContentAsync()).ReturnsAsync(string.Empty);

            var result = (await service.GetNewsAsync()).ToList();

            Assert.Empty(result);
            _store.Verify(
                store => store.SaveAsync(
                    new DateOnly(2026, 11, 17),
                    It.IsAny<IReadOnlyList<NewsArticle>>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldServeCachedResultsUntilForcedRefresh()
    {
        // Arrange
        var handler = new FakeHttpHandler(request => Ok(BuildPageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            // Act
            await service.GetNewsAsync();
            await service.GetNewsAsync();
            var requestsAfterCacheHits = handler.RequestCount;

            await service.GetNewsAsync(forceRefresh: true);

            // Assert
            Assert.Equal(Pages, requestsAfterCacheHits);
            Assert.Equal(Pages * 2, handler.RequestCount);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFallBackToBrowserWhenHomeHttpFails()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        try
        {
            // Act
            var result = (await service.GetNewsAsync()).ToList();

            // Assert
            Assert.Equal(Sources * ArticlesPerPage, result.Count);
            _mockBrowserType.Verify(
                bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()),
                Times.Once);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldKeepHomeArticlesWhenSectionsFail()
    {
        // Arrange
        var handler = new FakeHttpHandler(request =>
            request.RequestUri!.AbsolutePath == "/"
                ? Ok(BuildPageHtml(request.RequestUri))
                : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        try
        {
            // Act
            var result = (await service.GetNewsAsync()).ToList();

            // Assert
            Assert.Equal(Sources * ArticlesPerPage, result.Count);
            Assert.Equal(
                Sources,
                result.Select(article => article.SourceName).Distinct().Count());
            Assert.All(
                result.GroupBy(article => article.SourceName),
                group => Assert.Equal(ArticlesPerPage, group.Count()));
        }
        finally
        {
            service.Dispose();
        }
    }

    private NewsAggregationService CreateService(FakeHttpHandler handler) =>
        new(
            _mockPlaywright.Object,
            new HttpClient(handler),
            NullLogger<NewsAggregationService>.Instance,
            _store.Object,
            new EditionClock(_timeProvider, Options.Create(new ArchiveOptions())));

    private static HttpResponseMessage Ok(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };

    /// <summary>
    /// Minimal page with a JSON-LD ItemList, the most reliable structure found
    /// on the real Argentine news sites. The page path is part of every title and
    /// URL so home and section pages never collide.
    /// </summary>
    private static string BuildPageHtml(Uri page)
    {
        var pageName = page.AbsolutePath.Trim('/');
        if (pageName.Length == 0)
        {
            pageName = "portada";
        }

        var pageKey = pageName.Replace('/', '-');
        var items = string.Join(',', Enumerable.Range(1, ArticlesPerPage).Select(index => $$"""
            {
                "@type": "ListItem",
                "position": {{index}},
                "name": "Noticia de {{pageKey}} número {{index}} de {{page.Host}}",
                "description": "Descripción de la noticia de {{pageKey}} número {{index}} publicada por {{page.Host}}.",
                "url": "/{{pageKey}}/noticia-de-prueba-{{pageKey}}-{{index}}"
            }
            """));

        return $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "ItemList",
                    "itemListElement": [{{items}}]
                }
                </script>
            </head>
            <body></body>
            </html>
            """;
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage> _handler;
        private int _requestCount;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public void SetHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(_handler(request));
        }
    }
}
