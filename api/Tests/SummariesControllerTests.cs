using Microsoft.AspNetCore.Mvc;
using Moq;
using NewsAggregator.Controllers;
using NewsAggregator.Models;
using NewsAggregator.Services.Summaries;
using Xunit;

public class SummariesControllerTests
{
    private const string ArticleUrl = "https://www.clarin.com/nota";

    private static ArticleSummary StoredSummary() => new()
    {
        Url = ArticleUrl,
        Summary = "Un resumen guardado con la extensión suficiente para ser mostrado al lector.",
        Model = "gemma4:e2b",
        GeneratedAt = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc)
    };

    private static SummariesController CreateController(
        Mock<IArticleSummaryService> service)
    {
        return new SummariesController(service.Object);
    }

    [Fact]
    public async Task GetSummary_ShouldRejectUnsupportedUrls()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(It.IsAny<string>())).Returns(false);

        var result = await CreateController(service)
            .GetSummary("https://example.com/nota", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        service.Verify(
            item => item.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSummary_ShouldReturnNotFoundWhenNothingIsStored()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(ArticleUrl)).Returns(true);
        service.Setup(item => item.GetAsync(ArticleUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArticleSummary?)null);

        var result = await CreateController(service)
            .GetSummary(ArticleUrl, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetSummary_ShouldReturnTheStoredSummary()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(ArticleUrl)).Returns(true);
        service.Setup(item => item.GetAsync(ArticleUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StoredSummary());

        var result = await CreateController(service)
            .GetSummary(ArticleUrl, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(StoredSummary().Summary, Assert.IsType<ArticleSummary>(ok.Value).Summary);
    }

    [Fact]
    public async Task GenerateSummary_ShouldReturnTheGeneratedSummary()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(ArticleUrl)).Returns(true);
        service.Setup(item => item.GenerateAsync(ArticleUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryGenerationResult(
                SummaryGenerationStatus.Ok,
                StoredSummary(),
                null));

        var result = await CreateController(service)
            .GenerateSummary(ArticleUrl, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<ArticleSummary>(ok.Value);
    }

    [Fact]
    public async Task GenerateSummary_ShouldReturnNotFoundWithoutABody()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(ArticleUrl)).Returns(true);
        service.Setup(item => item.GenerateAsync(ArticleUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryGenerationResult(
                SummaryGenerationStatus.ContentUnavailable,
                null,
                "No se pudo extraer el contenido de la nota."));

        var result = await CreateController(service)
            .GenerateSummary(ArticleUrl, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(SummaryGenerationStatus.Disabled)]
    [InlineData(SummaryGenerationStatus.ModelUnavailable)]
    public async Task GenerateSummary_ShouldReturnServiceUnavailable(
        SummaryGenerationStatus status)
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(ArticleUrl)).Returns(true);
        service.Setup(item => item.GenerateAsync(ArticleUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryGenerationResult(status, null, "sin modelo"));

        var result = await CreateController(service)
            .GenerateSummary(ArticleUrl, CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, unavailable.StatusCode);
    }

    [Fact]
    public async Task GenerateSummary_ShouldReturnBadGatewayOnGenerationFailure()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(ArticleUrl)).Returns(true);
        service.Setup(item => item.GenerateAsync(ArticleUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryGenerationResult(
                SummaryGenerationStatus.GenerationFailed,
                null,
                "falló"));

        var result = await CreateController(service)
            .GenerateSummary(ArticleUrl, CancellationToken.None);

        var failed = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(502, failed.StatusCode);
    }

    [Fact]
    public async Task GenerateSummary_ShouldRejectUnsupportedUrls()
    {
        var service = new Mock<IArticleSummaryService>();
        service.Setup(item => item.IsSupportedUrl(It.IsAny<string>())).Returns(false);

        var result = await CreateController(service)
            .GenerateSummary("https://example.com/nota", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        service.Verify(
            item => item.GenerateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
