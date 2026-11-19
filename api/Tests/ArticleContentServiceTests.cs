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
}
