using NewsAggregator.Models;
using NewsAggregator.Services.Grouping;
using Xunit;

public class HeadlineSimilarityTests
{
    // Expected values were measured with Python's difflib over the real feeds
    // (240 headlines, 12 outlets). They pin the port, not the output.
    [Theory]
    [InlineData(
        "Feliz y emocionado, León XIV cerró su gira con una multitudinaria misa en Lima y superó una prueba de fuego para su salud",
        "León XIV se despidió de Perú con una misa multitudinaria y cerró su gira por Sudamérica: \"Los llevaré siempre conmigo\"",
        0.616610)]
    [InlineData(
        "Feliz y emocionado, León XIV cerró su gira con una multitudinaria misa en Lima y superó una prueba de fuego para su salud",
        "Terminó la gira del papa León XIV por Sudamérica y ya viaja rumbo a Italia: \"Los llevaré siempre conmigo\"",
        0.269785)]
    [InlineData(
        "En Perú. El papa León XIV recibió el golpe de un objeto mientras iba en el papamóvil",
        "VIDEO | Así fue el golpe que recibió el Papa León XIV en Perú",
        0.772858)]
    [InlineData(
        "“Papamanía”. León XIV cierra su histórica gira por América Latina, en la que reveló una faceta inesperada",
        "León XIV cerró su gira sudamericana con una emotiva despedida en Perú y una prueba de salud superada",
        0.284229)]
    [InlineData(
        "El dólar oficial extendió las subas y quebró un nuevo récord, mientras que el blue alcanzó los $1.600",
        "El dólar no afloja y comenzó la semana con un nuevo récord",
        0.505997)]
    [InlineData(
        "El papa León XIV fue golpeado en un ojo con un decenario cuando iba a dar su última misa en Perú",
        "Feliz y emocionado, León XIV cerró su gira con una multitudinaria misa en Lima y superó una prueba de fuego para su salud",
        0.305676)]
    public void TokenSetRatio_ReplicatesTheMeasuredFeedValues(string left, string right, double expected)
    {
        var actual = HeadlineSimilarity.TokenSetRatio(left, right);

        Assert.InRange(actual, expected - 0.0005, expected + 0.0005);
    }

    [Fact]
    public void TokenSetRatio_IsZeroForDifferentStories()
    {
        const string pope =
            "Feliz y emocionado, León XIV cerró su gira con una multitudinaria misa en Lima y superó una prueba de fuego para su salud";
        const string economy =
            "Caputo dijo que Milei ganará en primera vuelta y rechazó los pronósticos de un 2027 volátil";

        Assert.Equal(0, HeadlineSimilarity.TokenSetRatio(pope, economy));
    }

    [Fact]
    public void TokenSetRatio_IgnoresAccentsAndPunctuation()
    {
        const string withAccents = "León XIV cerró su gira multitudinaria en Perú";
        const string withoutAccents = "Leon XIV cerro su gira multitudinaria en Peru";

        Assert.Equal(1, HeadlineSimilarity.TokenSetRatio(withAccents, withoutAccents));
    }

    [Fact]
    public void SignificantWords_DropsStopWordsAndShortWords()
    {
        var words = HeadlineSimilarity.SignificantWords("El papa de la Argentina y su gira");

        Assert.Equal(new[] { "argentina", "gira", "papa" }, words.OrderBy(word => word));
    }
}

public class StoryGroupingServiceTests
{
    private static StoryGroupingService CreateService() =>
        new(Microsoft.Extensions.Logging.Abstractions.NullLogger<StoryGroupingService>.Instance);

    private static NewsArticle Article(string source, string title) => new()
    {
        SourceName = source,
        Title = title,
        OriginalUrl = $"https://example.com/{source}/{title.Length}"
    };

    [Fact]
    public void Group_MergesHeadlinesThatReportTheSameStory()
    {
        var articles = new[]
        {
            Article("Clarín", "León XIV cerró su gira multitudinaria en Lima"),
            Article("TN", "León XIV cerró su gira con una misa multitudinaria en Lima"),
            Article("Ámbito Financiero", "León XIV cerró su gira con una misa multitudinaria en Lima y Perú"),
            Article("La Voz del Interior", "Paro de colectivos: fracasó la reunión entre la UTA y los empresarios")
        };

        var stories = CreateService().Group(articles);

        Assert.Equal(2, stories.Count);
        var pope = stories[0];
        Assert.Equal(3, pope.SourceCount);
        Assert.Equal(3, pope.ArticleCount);
        Assert.Equal("Clarín", pope.SourceName);
        Assert.Equal(3, pope.Sources.Count);
    }

    [Fact]
    public void Group_KeepsUnrelatedHeadlinesApart()
    {
        var articles = new[]
        {
            Article("Clarín", "León XIV cerró su gira multitudinaria en Lima"),
            Article("TN", "El riesgo país se disparó arriba de los 850 puntos básicos"),
            Article("C5N", "Scaloni confirmó la vuelta de un titular contra Costa Rica")
        };

        var stories = CreateService().Group(articles);

        Assert.Equal(3, stories.Count);
        Assert.All(stories, story => Assert.Equal(1, story.SourceCount));
    }

    [Fact]
    public void Group_OrdersByCoverageThenByRealDate()
    {
        var older = new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 11, 16, 11, 0, 0, DateTimeKind.Utc);

        var articles = new[]
        {
            Article("Clarín", "El riesgo país se disparó arriba de los 850 puntos básicos"),
            Article("TN", "Scaloni confirmó la vuelta de un titular contra Costa Rica"),
            Article("Perfil", "Scaloni confirmó la vuelta de un titular contra Costa Rica y anticipó cambios")
        };

        articles[1].PublishedDate = older;
        articles[2].PublishedDate = newer;

        var stories = CreateService().Group(articles);

        Assert.Equal(2, stories[0].SourceCount);
        Assert.Equal("Scaloni", stories[0].Title.Split(' ')[0]);
        Assert.Equal(newer, stories[0].LatestPublishedDate);
        Assert.Equal(1, stories[1].SourceCount);
    }

    [Fact]
    public void Group_DoesNotInventPublicationDates()
    {
        var articles = new[]
        {
            Article("Clarín", "León XIV cerró su gira multitudinaria en Lima"),
            Article("TN", "León XIV cerró su gira con una misa multitudinaria en Lima")
        };

        var stories = CreateService().Group(articles);

        Assert.Null(stories[0].LatestPublishedDate);
        Assert.All(stories[0].Sources, source => Assert.Null(source.PublishedDate));
    }

    [Fact]
    public void Group_MergesTheDollarStoryAtTheMeasuredBoundary()
    {
        var articles = new[]
        {
            Article("Ámbito Financiero", "El dólar oficial extendió las subas y quebró un nuevo récord, mientras que el blue alcanzó los $1.600"),
            Article("C5N", "El dólar no afloja y comenzó la semana con un nuevo récord")
        };

        var stories = CreateService().Group(articles);

        Assert.Single(stories);
        Assert.Equal(2, stories[0].SourceCount);
    }

    [Fact]
    public void Group_KeepsThePopeHitApartFromThePopeFarewell()
    {
        var articles = new[]
        {
            Article("Clarín", "El papa León XIV fue golpeado en un ojo con un decenario cuando iba a dar su última misa en Perú"),
            Article("Clarín", "Feliz y emocionado, León XIV cerró su gira con una multitudinaria misa en Lima y superó una prueba de fuego para su salud")
        };

        var stories = CreateService().Group(articles);

        Assert.Equal(2, stories.Count);
        Assert.All(stories, story => Assert.Equal(1, story.SourceCount));
    }

    [Fact]
    public void Group_KeepsCoverageWithTwoVersionsPerOutlet()
    {
        var sources = new[]
        {
            "Clarín", "La Nación", "Infobae", "Página/12", "TN", "Ámbito Financiero",
            "El Cronista", "Perfil", "C5N", "La Voz del Interior", "El Día", "0221", "Seúl"
        };

        var articles = sources
            .SelectMany((source, index) => new[]
            {
                Article(source, "León XIV cerró su gira multitudinaria en Lima"),
                Article(source, $"León XIV cerró su gira multitudinaria en Lima y saludó a los fieles {index}")
            })
            .ToArray();

        var stories = CreateService().Group(articles);

        var story = Assert.Single(stories);
        Assert.Equal(13, story.SourceCount);
        Assert.Equal(26, story.ArticleCount);
    }

    [Fact]
    public void Group_ReturnsEmptyForNoArticles()
    {
        Assert.Empty(CreateService().Group(Array.Empty<NewsArticle>()));
    }
}
