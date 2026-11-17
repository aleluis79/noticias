using NewsAggregator.Models;
using NewsAggregator.Services.Tts;
using Xunit;

public class TtsTextBuilderTests
{
    private static ArticleContent Content() => new()
    {
        Url = "https://www.clarin.com/nota",
        Title = "  El título de la nota  ",
        Author = "Ana Pérez",
        Paragraphs = new[] { " Primer párrafo. ", "Segundo párrafo." }
    };

    [Fact]
    public void Build_Title_ReturnsNormalizedTitle()
    {
        var text = TtsTextBuilder.Build(
            new TtsSegment(TtsSegmentPart.Title, null),
            Content(),
            "Clarín");

        Assert.Equal("El título de la nota", text);
    }

    [Fact]
    public void Build_Title_PrefersTheVisibleTitleFromTheReader()
    {
        var text = TtsTextBuilder.Build(
            new TtsSegment(TtsSegmentPart.Title, null),
            Content(),
            "Clarín",
            titleOverride: "Título que ve el lector");

        Assert.Equal("Título que ve el lector", text);
    }

    [Fact]
    public void Build_Attribution_PrefersTheVisibleAuthorFromTheReader()
    {
        var text = TtsTextBuilder.Build(
            new TtsSegment(TtsSegmentPart.Attribution, null),
            Content(),
            "Clarín",
            authorOverride: "Otra Firma");

        Assert.Equal("Por Otra Firma, Clarín.", text);
    }

    [Fact]
    public void Build_Attribution_IncludesVisibleAuthorAndOutlet()
    {
        var text = TtsTextBuilder.Build(
            new TtsSegment(TtsSegmentPart.Attribution, null),
            Content(),
            "Clarín");

        Assert.Equal("Por Ana Pérez, Clarín.", text);
    }

    [Fact]
    public void Build_Attribution_OmitsGenericAuthorAndOutletAsAuthor()
    {
        var generic = Content();
        generic.Author = "Redacción";

        var outletAsAuthor = Content();
        outletAsAuthor.Author = "Clarín";

        Assert.Equal(
            "Clarín.",
            TtsTextBuilder.Build(
                new TtsSegment(TtsSegmentPart.Attribution, null),
                generic,
                "Clarín"));
        Assert.Equal(
            "Clarín.",
            TtsTextBuilder.Build(
                new TtsSegment(TtsSegmentPart.Attribution, null),
                outletAsAuthor,
                "Clarín"));
    }

    [Fact]
    public void Build_Body_ReturnsTheRequestedParagraph()
    {
        var text = TtsTextBuilder.Build(
            new TtsSegment(TtsSegmentPart.Body, 1),
            Content(),
            "Clarín");

        Assert.Equal("Segundo párrafo.", text);
    }

    [Fact]
    public void Build_Body_WithInvalidIndex_ReturnsEmpty()
    {
        var text = TtsTextBuilder.Build(
            new TtsSegment(TtsSegmentPart.Body, 99),
            Content(),
            "Clarín");

        Assert.Equal(string.Empty, text);
    }

    [Fact]
    public void CacheKey_IsStableAndChangesWithScopeOrModel()
    {
        const string model = "/voices/es_AR-daniela-high.onnx";

        var first = TtsCacheKey.For("url|title", model);
        var second = TtsCacheKey.For("url|title", model);

        Assert.Equal(first, second);
        Assert.Matches("^[a-f0-9]{64}$", first);
        Assert.NotEqual(first, TtsCacheKey.For("url|body|0", model));
        Assert.NotEqual(first, TtsCacheKey.For("url|title", "/voices/other.onnx"));
    }
}
