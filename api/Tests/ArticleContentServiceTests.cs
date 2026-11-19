using NewsAggregator.Services;
using Xunit;

public class ArticleContentServiceTests
{
    [Fact]
    public void ExtractParagraphs_ShouldPreferTheArticleBodyAndSkipNoise()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <nav>
                    <p>Inicio Últimas noticias Política Economía Deportes Espectáculos Editoriales</p>
                </nav>
                <main>
                    <article>
                        <header>
                            <h1>Una nota de prueba</h1>
                            <p>Esta bajada pertenece al encabezado y no debería aparecer dentro del cuerpo de la nota.</p>
                        </header>
                        <div class="article-body">
                            <p>El primer párrafo del cuerpo de la nota tiene el texto suficiente para ser considerado contenido real y verificable por cualquier lector que llegue al sitio.</p>
                            <p>El segundo párrafo también es suficientemente largo como para formar parte de la extracción final del cuerpo de la nota periodística publicada hoy.</p>
                            <div class="related-notes">
                                <p>Nota relacionada: otro título que no debe formar parte del cuerpo de la nota publicada.</p>
                            </div>
                            <p>Leé también: una nota recomendada que tampoco debe incluirse dentro del cuerpo extraído.</p>
                        </div>
                    </article>
                </main>
            </body>
            </html>
            """;

        var paragraphs = ArticleContentService.ExtractParagraphs(html);

        Assert.Equal(2, paragraphs.Count);
        Assert.StartsWith("El primer párrafo", paragraphs[0]);
        Assert.StartsWith("El segundo párrafo", paragraphs[1]);
        Assert.DoesNotContain(paragraphs, paragraph => paragraph.Contains("bajada"));
        Assert.DoesNotContain(paragraphs, paragraph => paragraph.Contains("relacionada"));
        Assert.DoesNotContain(paragraphs, paragraph => paragraph.Contains("Leé también"));
    }

    [Fact]
    public void ExtractParagraphs_ShouldFallBackToTheJsonLdArticleBody()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "NewsArticle",
                    "headline": "Nota estructurada",
                    "articleBody": "Primer párrafo del cuerpo estructurado, con la extensión necesaria para ser considerado contenido real y verificable por cualquier lector que llegue al sitio.\n\nSegundo párrafo del cuerpo estructurado, que también tiene una longitud razonable para la extracción final del cuerpo de la nota periodística publicada hoy."
                }
                </script>
            </head>
            <body>
                <div id="app"></div>
            </body>
            </html>
            """;

        var paragraphs = ArticleContentService.ExtractParagraphs(html);

        Assert.Equal(2, paragraphs.Count);
        Assert.StartsWith("Primer párrafo", paragraphs[0]);
        Assert.StartsWith("Segundo párrafo", paragraphs[1]);
    }

    [Fact]
    public void ExtractAuthor_ShouldReadTheJsonLdAuthor()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "NewsArticle",
                    "headline": "Nota estructurada",
                    "author": { "@type": "Person", "name": "Jazmín Bullorini" }
                }
                </script>
            </head>
            <body></body>
            </html>
            """;

        var author = ArticleContentService.ExtractAuthor(html);

        Assert.Equal("Jazmín Bullorini", author);
    }

    [Fact]
    public void ExtractAuthor_ShouldReadTheMetaAuthorWhenThereIsNoStructuredData()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta name="author" content="Mara Pedrazzoli">
            </head>
            <body></body>
            </html>
            """;

        var author = ArticleContentService.ExtractAuthor(html);

        Assert.Equal("Mara Pedrazzoli", author);
    }

    [Fact]
    public void ExtractAuthor_ShouldReadTheVisibleByline()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <article>
                    <div class="article__author">
                        <span class="article__author">Nicolás Gonzalez Dior</span>
                        <div>Periodista - Sección Política</div>
                    </div>
                </article>
            </body>
            </html>
            """;

        var author = ArticleContentService.ExtractAuthor(html);

        Assert.Equal("Nicolás Gonzalez Dior", author);
    }

    [Fact]
    public void ExtractAuthor_ShouldIgnoreNewsroomBylines()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <p class="autor__firmante">Por <strong>Redacción</strong></p>
            </body>
            </html>
            """;

        var author = ArticleContentService.ExtractAuthor(html);

        Assert.Null(author);
    }

    [Fact]
    public void ExtractAuthor_ShouldReturnNullWhenThereIsNoAuthor()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <article>
                    <p>Texto de la nota sin firma publicada por el medio.</p>
                </article>
            </body>
            </html>
            """;

        var author = ArticleContentService.ExtractAuthor(html);

        Assert.Null(author);
    }

    [Fact]
    public void ExtractParagraphs_ShouldReturnEmptyWhenThereIsNoUsableText()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <nav><p>Inicio Política Economía Deportes Espectáculos</p></nav>
                <footer><p>Todos los derechos reservados</p></footer>
            </body>
            </html>
            """;

        var paragraphs = ArticleContentService.ExtractParagraphs(html);

        Assert.Empty(paragraphs);
    }

    [Fact]
    public void ExtractTitle_ShouldPreferTheOpenGraphTitle()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <title>Título del sitio | Sección</title>
                <meta property="og:title" content="El título real de la nota">
            </head>
            <body>
                <h1>Un encabezado visible distinto</h1>
            </body>
            </html>
            """;

        var title = ArticleContentService.ExtractTitle(html);

        Assert.Equal("El título real de la nota", title);
    }

    [Fact]
    public void ExtractTitle_ShouldFallBackToTheVisibleHeading()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <title>Título del sitio</title>
            </head>
            <body>
                <h1>Un encabezado visible</h1>
            </body>
            </html>
            """;

        var title = ArticleContentService.ExtractTitle(html);

        Assert.Equal("Un encabezado visible", title);
    }

    [Fact]
    public void ExtractTitle_ShouldReturnNullWhenThereIsNoTitle()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <article><p>Una nota sin título.</p></article>
            </body>
            </html>
            """;

        var title = ArticleContentService.ExtractTitle(html);

        Assert.Null(title);
    }

    [Fact]
    public void IsSeulRegistrationWall_ShouldOnlyMatchTheSignupWidget()
    {
        const string freePage = """
            <html>
            <head>
                <link rel="stylesheet" id="seul-paywall-css" href="https://seul.ar/assets/acceso-restringido.css" />
                <script id="seul-paywall-js" src="https://seul.ar/assets/acceso-restringido.js"></script>
            </head>
            <body><article><p>El cuerpo real de la nota.</p></article></body>
            </html>
            """;

        const string gatedPage = """
            <html>
            <body>
                <div class="seul-paywall" data-seul-widget>
                    <h2 class="seul-paywall__titulo">Seguí leyendo. Es gratis.</h2>
                    <p class="seul-paywall__subtitulo">Creá tu cuenta y accedé a todos los artículos de Seúl.</p>
                </div>
            </body>
            </html>
            """;

        Assert.False(ArticleContentService.IsSeulRegistrationWall(freePage));
        Assert.True(ArticleContentService.IsSeulRegistrationWall(gatedPage));
    }

    [Fact]
    public void ExtractFeedParagraphs_ShouldReturnTheFullBodyAndDropTheFeedFooter()
    {
        const string feed = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/">
                <channel>
                    <item>
                        <title>La revancha de los normies</title>
                        <link>https://seul.ar/la-revancha-de-los-normies/</link>
                        <pubDate>Thu, 05 Nov 2026 03:29:06 +0000</pubDate>
                        <content:encoded><![CDATA[
                            <p>Primer párrafo completo del cuerpo de la nota, publicado en el RSS sin el muro de registro que aparece en el sitio.</p>
                            <p>Segundo párrafo completo del cuerpo de la nota, también recuperado desde el feed público del medio.</p>
                            <p>La entrada La revancha de los normies se publicó primero en Seúl .</p>
                        ]]></content:encoded>
                    </item>
                </channel>
            </rss>
            """;

        var paragraphs = ArticleContentService.ExtractFeedParagraphs(
            feed,
            "https://seul.ar/la-revancha-de-los-normies/");

        Assert.Equal(2, paragraphs.Count);
        Assert.StartsWith("Primer párrafo", paragraphs[0]);
        Assert.StartsWith("Segundo párrafo", paragraphs[1]);
        Assert.DoesNotContain(paragraphs, paragraph => paragraph.Contains("La entrada"));
        Assert.DoesNotContain(paragraphs, paragraph => paragraph.Contains("Creá tu cuenta"));
    }

    [Fact]
    public void ExtractFeedParagraphs_ShouldIgnoreAnEntryThatOnlyHasTheSignupWall()
    {
        const string feed = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0" xmlns:content="http://purl.org/rss/1.0/modules/content/">
                <channel>
                    <item>
                        <link>https://seul.ar/nota-bloqueada/</link>
                        <content:encoded><![CDATA[
                            <div class="seul-paywall" data-seul-widget>
                                <h2 class="seul-paywall__titulo">Seguí leyendo. Es gratis.</h2>
                                <p>Creá tu cuenta y accedé a todos los artículos de Seúl.</p>
                            </div>
                        ]]></content:encoded>
                    </item>
                </channel>
            </rss>
            """;

        var paragraphs = ArticleContentService.ExtractFeedParagraphs(
            feed,
            "https://seul.ar/nota-bloqueada/");

        Assert.Empty(paragraphs);
    }

    [Fact]
    public void ExtractPublishedDate_ShouldReadTheStructuredData()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "Article",
                    "headline": "La revancha de los normies",
                    "datePublished": "2026-11-05T03:29:06+00:00"
                }
                </script>
            </head>
            <body></body>
            </html>
            """;

        var date = ArticleContentService.ExtractPublishedDate(html);

        Assert.NotNull(date);
        Assert.Equal(2026, date!.Value.Year);
        Assert.Equal(11, date.Value.Month);
        Assert.Equal(5, date.Value.Day);
    }

    [Fact]
    public void ExtractPublishedDate_ShouldReadTheMetaTag()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta property="article:published_time" content="2025-05-25T07:00:14+00:00" />
            </head>
            <body></body>
            </html>
            """;

        var date = ArticleContentService.ExtractPublishedDate(html);

        Assert.NotNull(date);
        Assert.Equal(2025, date!.Value.Year);
        Assert.Equal(5, date.Value.Month);
        Assert.Equal(25, date.Value.Day);
    }
}
