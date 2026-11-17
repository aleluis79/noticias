using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.Services.Grouping;
using NewsAggregator.Services.Tts;
using Xunit;

public class NewsControllerAudioTests
{
    private const string ArticleUrl = "https://www.clarin.com/nota";

    private static ArticleContent Content() => new()
    {
        Url = ArticleUrl,
        Title = "El título",
        Author = "Ana Pérez",
        Paragraphs = new[] { "Primer párrafo.", "Segundo párrafo." }
    };

    private static (NewsController Controller, Mock<IArticleContentService> Content, Mock<ITextToSpeechService> Tts)
        CreateController(
            bool urlSupported = true,
            ArticleContent? article = null,
            bool ttsEnabled = true,
            TtsAudio? audio = null)
    {
        var content = new Mock<IArticleContentService>();
        content.Setup(service => service.IsSupportedUrl(It.IsAny<string>()))
            .Returns(urlSupported);
        content.Setup(service => service.GetArticleContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(article ?? Content());

        var tts = new Mock<ITextToSpeechService>();
        tts.SetupGet(service => service.IsEnabled).Returns(ttsEnabled);
        tts.Setup(service => service.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(audio ?? new TtsAudio(new byte[] { 1, 2, 3 }, "audio/mpeg"));

        var controller = new NewsController(
            Mock.Of<INewsAggregationService>(),
            content.Object,
            Mock.Of<IStoryGroupingService>(),
            tts.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return (controller, content, tts);
    }

    [Fact]
    public async Task GetAudio_WithUnsupportedUrl_ReturnsBadRequestWithoutFetching()
    {
        var (controller, content, tts) = CreateController(urlSupported: false);

        var result = await controller.GetAudio("https://example.com/nota", "title", null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        content.Verify(
            service => service.GetArticleContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        tts.Verify(
            service => service.SynthesizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("otra")]
    [InlineData("")]
    public async Task GetAudio_WithUnknownPart_ReturnsBadRequest(string part)
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetAudio(ArticleUrl, part, null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAudio_BodyWithoutIndex_ReturnsBadRequest()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetAudio(ArticleUrl, "body", null, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAudio_BodyWithIndexOutOfRange_ReturnsBadRequest()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetAudio(ArticleUrl, "body", 5, null, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAudio_WhenTtsIsDisabled_ReturnsServiceUnavailable()
    {
        var (controller, content, _) = CreateController(ttsEnabled: false);

        var result = await controller.GetAudio(ArticleUrl, "title", null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
        content.Verify(
            service => service.GetArticleContentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAudio_ReturnsPlayableAudioWithCachingHeaders()
    {
        var (controller, _, _) = CreateController();

        var result = await controller.GetAudio(ArticleUrl, "title", null, null, null, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("audio/mpeg", file.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal("public, max-age=86400", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetAudio_BuildsAttributionWithTheOutletFromTheUrl()
    {
        var (controller, _, tts) = CreateController();
        string? capturedText = null;
        string? capturedScope = null;
        tts.Setup(service => service.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((scope, text, _) =>
            {
                capturedScope = scope;
                capturedText = text;
            })
            .ReturnsAsync(new TtsAudio(new byte[] { 1 }, "audio/mpeg"));

        await controller.GetAudio(ArticleUrl, "attribution", null, null, null, CancellationToken.None);

        Assert.Equal("Por Ana Pérez, Clarín.", capturedText);
        Assert.StartsWith("https://www.clarin.com/nota|attribution|", capturedScope);
    }

    [Fact]
    public async Task GetAudio_UsesTheTitleAndAuthorShownByTheReader()
    {
        var (controller, _, tts) = CreateController();
        var captured = new List<string>();
        tts.Setup(service => service.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => captured.Add(text))
            .ReturnsAsync(new TtsAudio(new byte[] { 1 }, "audio/mpeg"));

        await controller.GetAudio(ArticleUrl, "title", null, "Título visible", null, CancellationToken.None);
        await controller.GetAudio(ArticleUrl, "attribution", null, null, "Otra Firma", CancellationToken.None);

        Assert.Equal("Título visible", captured[0]);
        Assert.Equal("Por Otra Firma, Clarín.", captured[1]);
    }

    [Fact]
    public async Task GetAudio_BuildsTheRequestedBodyParagraph()
    {
        var (controller, _, tts) = CreateController();
        string? capturedText = null;
        tts.Setup(service => service.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => capturedText = text)
            .ReturnsAsync(new TtsAudio(new byte[] { 1 }, "audio/mpeg"));

        await controller.GetAudio(ArticleUrl, "body", 1, null, null, CancellationToken.None);

        Assert.Equal("Segundo párrafo.", capturedText);
    }

    [Fact]
    public async Task GetAudio_WhenEngineReturnsNull_ReturnsServiceUnavailable()
    {
        var (controller, _, tts) = CreateController();
        tts.Setup(service => service.SynthesizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TtsAudio?)null);

        var result = await controller.GetAudio(ArticleUrl, "title", null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }
}
