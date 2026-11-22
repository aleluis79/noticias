using NewsAggregator.Models;
using NewsAggregator.Services;
using Xunit;

public class ArticleMediaTests
{
    private const string LongParagraph1 =
        "El primer párrafo del cuerpo de la nota tiene el texto suficiente para ser considerado contenido real y verificable por cualquier lector que llegue al sitio.";

    private const string LongParagraph2 =
        "El segundo párrafo también es suficientemente largo como para formar parte de la extracción final del cuerpo de la nota periodística publicada hoy.";

    [Fact]
    public void ExtractMedia_ShouldReadAFigureWithCaptionAndPosition()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <article>
                    <div class="article-body">
                        <p>{LongParagraph1}</p>
                        <figure>
                            <img src="https://cdn.example.com/fotos/estadio.jpg"
                                 alt="El estadio lleno"
                                 width="1200"
                                 height="800" />
                            <figcaption>La hinchada durante el partido. (Foto: Reuters)</figcaption>
                        </figure>
                        <p>{LongParagraph2}</p>
                    </div>
                </article>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.infobae.com/nota");

        var image = Assert.Single(media);
        Assert.Equal("image", image.Type);
        Assert.Equal("https://cdn.example.com/fotos/estadio.jpg", image.Url);
        Assert.Equal("La hinchada durante el partido. (Foto: Reuters)", image.Caption);
        Assert.Equal("El estadio lleno", image.Alt);
        Assert.Equal(1200, image.Width);
        Assert.Equal(800, image.Height);
        Assert.Equal(1, image.AfterParagraph);
    }

    [Fact]
    public void ExtractMedia_ShouldResolveRelativeAndLazySources()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div id="cuerpo">
                    <p>{LongParagraph1}</p>
                    <img data-src="/img/2026/11/22/foto.jpg"
                         data-srcset="/img/2026/11/22/foto-400.jpg 400w, /img/2026/11/22/foto-1200.jpg 1200w"
                         alt="Una foto publicada por Clarín" />
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(
            html,
            "https://www.clarin.com/deportes/nota_0_abc.html");

        var image = Assert.Single(media);
        Assert.Equal("https://www.clarin.com/img/2026/11/22/foto-1200.jpg", image.Url);
        Assert.Equal("Una foto publicada por Clarín", image.Alt);
        Assert.Equal("Una foto publicada por Clarín", image.Caption);
        Assert.Equal(1, image.AfterParagraph);
    }

    [Fact]
    public void ExtractMedia_ShouldPreferTheLargestSrcsetCandidate()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div class="nota-cuerpo">
                    <p>{LongParagraph1}</p>
                    <img src="https://cdn.example.com/foto-800.jpg"
                         srcset="https://cdn.example.com/foto-400.jpg 400w,
                                 https://cdn.example.com/foto-1600.jpg 1600w,
                                 https://cdn.example.com/foto-800.jpg 800w"
                         alt="Foto con varios tamaños" />
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.lanacion.com.ar/nota");

        var image = Assert.Single(media);
        Assert.Equal("https://cdn.example.com/foto-1600.jpg", image.Url);
    }

    [Fact]
    public void ExtractMedia_ShouldDiscardTrackingLogosPlaceholdersAndTinyImages()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div class="article-body">
                    <p>{LongParagraph1}</p>
                    <img src="https://sb.scorecardresearch.com/p?c1=2&amp;c2=6906398" alt="" />
                    <img src="https://cdn.example.com/assets/logo-clarin.png" width="300" height="120" alt="Clarín" />
                    <img src="https://cdn.example.com/assets/placeholder.jpg" alt="Placeholder" />
                    <img src="https://cdn.example.com/assets/fallback-promo-image-1200x630.png" alt="Fallback" />
                    <img src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==" alt="" />
                    <img src="https://cdn.example.com/fotos/pixel.gif" width="1" height="1" alt="" />
                    <img src="https://cdn.example.com/fotos/buena.jpg" width="800" height="600" alt="La foto que sí importa" />
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.clarin.com/nota");

        var image = Assert.Single(media);
        Assert.Equal("https://cdn.example.com/fotos/buena.jpg", image.Url);
    }

    [Fact]
    public void ExtractMedia_ShouldFallBackToTheOpenGraphImage()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <meta property="og:image" content="https://cdn.example.com/portada/nota.jpg" />
                <meta property="og:image:width" content="1200" />
                <meta property="og:image:height" content="800" />
            </head>
            <body>
                <article>
                    <div class="article-body">
                        <p>{LongParagraph1}</p>
                        <p>{LongParagraph2}</p>
                    </div>
                </article>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.lanacion.com.ar/nota");

        var image = Assert.Single(media);
        Assert.Equal("https://cdn.example.com/portada/nota.jpg", image.Url);
        Assert.Equal(0, image.AfterParagraph);
        Assert.Equal(1200, image.Width);
        Assert.Equal(800, image.Height);
    }

    [Fact]
    public void ExtractMedia_ShouldFallBackToTheJsonLdImage()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <head>
                <script type="application/ld+json">
                {
                    "@context": "https://schema.org",
                    "@type": "NewsArticle",
                    "headline": "Nota con imagen estructurada",
                    "image": "https://cdn.example.com/estructurada.jpg"
                }
                </script>
            </head>
            <body>
                <article>
                    <div class="article-body">
                        <p>El primer párrafo del cuerpo estructurado tiene el texto suficiente para ser considerado contenido real y verificable por cualquier lector que llegue al sitio.</p>
                        <p>El segundo párrafo del cuerpo estructurado también tiene una longitud razonable para la extracción final de la nota periodística publicada hoy.</p>
                    </div>
                </article>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.lanacion.com.ar/nota");

        var image = Assert.Single(media);
        Assert.Equal("https://cdn.example.com/estructurada.jpg", image.Url);
        Assert.Equal(0, image.AfterParagraph);
    }

    [Fact]
    public void ExtractMedia_ShouldReadNativeYouTubeAndVimeoVideos()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div class="article-body">
                    <p>{LongParagraph1}</p>
                    <video poster="/videos/poster.jpg">
                        <source src="/videos/nota.mp4" type="video/mp4" />
                    </video>
                    <iframe src="https://www.youtube.com/embed/abc123XYZ"></iframe>
                    <iframe src="https://player.vimeo.com/video/987654"></iframe>
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.infobae.com/nota");

        Assert.Equal(3, media.Count);

        Assert.Equal("video", media[0].Type);
        Assert.Equal("native", media[0].Provider);
        Assert.Equal("https://www.infobae.com/videos/nota.mp4", media[0].Url);
        Assert.Equal("https://www.infobae.com/videos/poster.jpg", media[0].PosterUrl);
        Assert.Equal(1, media[0].AfterParagraph);

        Assert.Equal("videoEmbed", media[1].Type);
        Assert.Equal("youtube", media[1].Provider);
        Assert.Equal("https://www.youtube.com/watch?v=abc123XYZ", media[1].Url);
        Assert.Equal(1, media[1].AfterParagraph);

        Assert.Equal("videoEmbed", media[2].Type);
        Assert.Equal("vimeo", media[2].Provider);
        Assert.Equal("https://vimeo.com/987654", media[2].Url);
        Assert.Equal(1, media[2].AfterParagraph);
    }

    [Fact]
    public void ExtractMedia_ShouldPlaceMediaBeforeTheFirstAndAfterTheLastParagraph()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div class="article-body">
                    <figure><img src="https://cdn.example.com/portada.jpg" alt="Portada" /></figure>
                    <p>{LongParagraph1}</p>
                    <p>{LongParagraph2}</p>
                    <figure><img src="https://cdn.example.com/cierre.jpg" alt="Cierre" /></figure>
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.clarin.com/nota");

        Assert.Equal(2, media.Count);
        Assert.Equal(0, media[0].AfterParagraph);
        Assert.Equal(2, media[1].AfterParagraph);
    }

    [Fact]
    public void ExtractMedia_ShouldReturnEmptyWhenThereIsNoMedia()
    {
        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div class="article-body">
                    <p>{LongParagraph1}</p>
                    <p>{LongParagraph2}</p>
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.clarin.com/nota");

        Assert.Empty(media);
    }

    [Fact]
    public void ExtractMedia_ShouldDeduplicateTheSameImageAndRespectTheLimit()
    {
        var figures = string.Concat(
            Enumerable.Range(0, 15).Select(index => $"""
                <figure><img src="https://cdn.example.com/fotos/{index}.jpg?width=800" alt="Foto {index}" /></figure>
                <figure><img src="https://cdn.example.com/fotos/{index}.jpg?width=1600" alt="Foto {index}" /></figure>
                """));

        var html = $"""
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div class="article-body">
                    <p>{LongParagraph1}</p>
                    {figures}
                </div>
            </body>
            </html>
            """;

        var media = ArticleContentService.ExtractMedia(html, "https://www.clarin.com/nota");

        Assert.Equal(12, media.Count);
        Assert.Equal(12, media.Select(item => item.Url).Distinct().Count());
        Assert.All(media, item => Assert.EndsWith("?width=1600", item.Url));
    }
}
