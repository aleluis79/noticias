using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Playwright;
using Moq;
using NewsAggregator.Models;
using NewsAggregator.Services;
using Xunit;

public class NewsAggregationServiceTests
{
    private const int Sources = 13;
    private const int ArticlesPerSource = 2;

    private readonly Mock<IPlaywright> _mockPlaywright = new();
    private readonly Mock<IBrowserType> _mockBrowserType = new();
    private readonly Mock<IBrowser> _mockBrowser = new();
    private readonly Mock<IBrowserContext> _mockContext = new();
    private readonly Mock<IPage> _mockPage = new();
    private readonly Mock<IResponse> _mockResponse = new();

    public NewsAggregationServiceTests()
    {
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
            .ReturnsAsync(() => BuildHomePageHtml(new Uri("https://fallback.example.com")));
        _mockPage.Setup(p => p.CloseAsync(It.IsAny<PageCloseOptions>())).Returns(Task.CompletedTask);
        _mockResponse.Setup(r => r.Ok).Returns(true);
    }

    [Fact]
    public async Task GetNewsAsync_ShouldParseEverySourceOverHttp()
    {
        // Arrange
        var handler = new FakeHttpHandler(request => Ok(BuildHomePageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            // Act
            var result = (await service.GetNewsAsync()).ToList();

            // Assert
            Assert.Equal(Sources * ArticlesPerSource, result.Count);
            Assert.Equal(Sources, handler.RequestCount);
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
    public async Task GetNewsAsync_ShouldServeCachedResultsUntilForcedRefresh()
    {
        // Arrange
        var handler = new FakeHttpHandler(request => Ok(BuildHomePageHtml(request.RequestUri!)));
        var service = CreateService(handler);

        try
        {
            // Act
            await service.GetNewsAsync();
            await service.GetNewsAsync();
            var requestsAfterCacheHits = handler.RequestCount;

            await service.GetNewsAsync(forceRefresh: true);

            // Assert
            Assert.Equal(Sources, requestsAfterCacheHits);
            Assert.Equal(Sources * 2, handler.RequestCount);
        }
        finally
        {
            service.Dispose();
        }
    }

    [Fact]
    public async Task GetNewsAsync_ShouldFallBackToBrowserWhenHttpFails()
    {
        // Arrange
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        try
        {
            // Act
            var result = (await service.GetNewsAsync()).ToList();

            // Assert
            Assert.Equal(Sources * ArticlesPerSource, result.Count);
            _mockBrowserType.Verify(
                bt => bt.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions>()),
                Times.Once);
        }
        finally
        {
            service.Dispose();
        }
    }

    private NewsAggregationService CreateService(FakeHttpHandler handler) =>
        new(_mockPlaywright.Object, new HttpClient(handler), NullLogger<NewsAggregationService>.Instance);

    private static HttpResponseMessage Ok(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };

    /// <summary>
    /// Minimal home page with a JSON-LD ItemList, the most reliable structure found
    /// on the real Argentine news sites.
    /// </summary>
    private static string BuildHomePageHtml(Uri source)
    {
        var items = string.Join(',', Enumerable.Range(1, ArticlesPerSource).Select(index => $$"""
            {
                "@type": "ListItem",
                "position": {{index}},
                "name": "Noticia de prueba número {{index}} de {{source.Host}}",
                "description": "Descripción de la noticia de prueba número {{index}} publicada por {{source.Host}}.",
                "url": "/politica/noticia-de-prueba-{{index}}"
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
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        private int _requestCount;

        public FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(_handler(request));
        }
    }
}
