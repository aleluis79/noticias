using NewsAggregator.Services.Parsers;
using Xunit;

public class HomePageParserTests
{
    private sealed class TestParser : HomePageParser
    {
        public override string SourceName => "Test News";

        public override string BaseUrl => "https://www.example.com";

        protected override string[] SectionPaths => new[] { "/politica", "/economia" };
    }

    [Fact]
    public void Parse_ShouldExtractItemsFromJsonLdItemList()
    {
        // Arrange
        const string html = """
            <html>
            <head>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@graph": [
                        {
                            "@type": "ItemList",
                            "itemListElement": [
                                {
                                    "@type": "ListItem",
                                    "position": 1,
                                    "name": "Primera noticia importante del día",
                                    "description": "Un resumen suficientemente largo para ser considerado una bajada válida.",
                                    "url": "https://www.example.com/politica/primera-noticia-importante"
                                },
                                {
                                    "@type": "ListItem",
                                    "position": 2,
                                    "name": "Segunda noticia importante del día",
                                    "url": "/economia/segunda-noticia-importante"
                                }
                            ]
                        }
                    ]
                }
                </script>
            </head>
            <body></body>
            </html>
            """;

        // Act
        var articles = new TestParser().Parse(html);

        // Assert
        Assert.Equal(2, articles.Count);
        Assert.Equal("Primera noticia importante del día", articles[0].Title);
        Assert.Equal("https://www.example.com/politica/primera-noticia-importante", articles[0].OriginalUrl);
        Assert.Equal("Un resumen suficientemente largo para ser considerado una bajada válida.", articles[0].Summary);
        Assert.Equal("Test News", articles[0].SourceName);
        Assert.Equal("https://www.example.com/economia/segunda-noticia-importante", articles[1].OriginalUrl);
    }

    [Fact]
    public void Parse_ShouldExtractArticleCardsAndIgnoreKickers()
    {
        // Arrange
        const string html = """
            <html>
            <body>
                <article class="story-card">
                    <h2 class="story-card__headline">
                        <a href="/politica/una-noticia-de-prueba-larga">
                            <span class="story-card__headline-kicker">Alerta Cedear.</span>
                            <span class="story-card__headline-text">Una noticia de prueba larga y relevante</span>
                        </a>
                    </h2>
                    <p>Esta es la bajada de la noticia con la longitud mínima necesaria para ser utilizada.</p>
                </article>
            </body>
            </html>
            """;

        // Act
        var articles = new TestParser().Parse(html);

        // Assert
        var article = Assert.Single(articles);
        Assert.Equal("Una noticia de prueba larga y relevante", article.Title);
        Assert.Equal("https://www.example.com/politica/una-noticia-de-prueba-larga", article.OriginalUrl);
        Assert.StartsWith("Esta es la bajada", article.Summary);
    }

    [Fact]
    public void Parse_ShouldIgnoreNavigationExternalLinksAndTheHomePage()
    {
        // Arrange
        const string html = """
            <html>
            <body>
                <nav>
                    <a href="/">Inicio</a>
                    <a href="/tag/politica">Política</a>
                    <a href="/suscribite">Suscribite al newsletter de noticias</a>
                </nav>
                <main>
                    <a href="https://otro-sitio.com/politica/noticia-externa-larga">Noticia publicada en otro sitio externo</a>
                    <a href="/politica/noticia-valida-de-prueba">Una noticia válida de prueba del sitio</a>
                </main>
            </body>
            </html>
            """;

        // Act
        var articles = new TestParser().Parse(html);

        // Assert
        var article = Assert.Single(articles);
        Assert.Equal("Una noticia válida de prueba del sitio", article.Title);
        Assert.Equal("https://www.example.com/politica/noticia-valida-de-prueba", article.OriginalUrl);
    }

    [Fact]
    public void Parse_ShouldIgnoreKickerHeadingsAndUseSubheadsAsSummary()
    {
        // Arrange
        const string html = """
            <html>
            <body>
                <article>
                    <div class="volanta-container">
                        <h3 class="card__volanta">TN junto a los inundados</h3>
                    </div>
                    <header>
                        <h2 class="card__headline">
                            <a href="/sociedad/2026/11/15/el-supernino-golpea-el-litoral">El Superniño golpea el Litoral y crece la preocupación</a>
                        </h2>
                        <h2 class="subhead">Diez ciudades mantienen sus ríos por encima del nivel de evacuación mientras avanza el operativo.</h2>
                    </header>
                </article>
            </body>
            </html>
            """;

        // Act
        var articles = new TestParser().Parse(html);

        // Assert
        var article = Assert.Single(articles);
        Assert.Equal("El Superniño golpea el Litoral y crece la preocupación", article.Title);
        Assert.StartsWith("Diez ciudades mantienen sus ríos", article.Summary);
    }

    [Fact]
    public void Parse_ShouldRespectAnExplicitArticleLimit()
    {
        // Arrange
        const string html = """
            <html>
            <body>
                <a href="/politica/primera-noticia-de-prueba">Primera noticia de prueba del día</a>
                <a href="/politica/segunda-noticia-de-prueba">Segunda noticia de prueba del día</a>
                <a href="/politica/tercera-noticia-de-prueba">Tercera noticia de prueba del día</a>
            </body>
            </html>
            """;

        // Act
        var articles = new TestParser().Parse(html, 1);

        // Assert
        var article = Assert.Single(articles);
        Assert.Equal("Primera noticia de prueba del día", article.Title);
    }

    [Fact]
    public void SectionUrls_ShouldResolvePathsAgainstTheBaseUrl()
    {
        // Act
        var urls = new TestParser().SectionUrls;

        // Assert
        Assert.Equal(
            new[]
            {
                "https://www.example.com/politica",
                "https://www.example.com/economia"
            },
            urls);
    }

    [Fact]
    public void Parse_ShouldReturnEmptyListForEmptyHtml()
    {
        Assert.Empty(new TestParser().Parse(string.Empty));
    }
}
