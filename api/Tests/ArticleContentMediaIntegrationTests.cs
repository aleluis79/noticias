using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using NewsAggregator.Services;
using NewsAggregator.Services.Archive;
using Xunit;

public class ArticleContentMediaIntegrationTests
{
    private const string ArticleUrl = "https://www.clarin.com/deportes/nota_0_abc.html";

    [Fact]
    public async Task GetArticleContentAsync_ShouldReturnMediaAndParagraphs()
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="es">
            <body>
                <div id="cuerpo">
                    <p>El primer párrafo del cuerpo de la nota tiene el texto suficiente para ser considerado contenido real y verificable por cualquier lector que llegue al sitio.</p>
                    <figure>
                        <img src="/img/foto.jpg" width="1200" height="800" alt="Una foto de la nota" />
                        <figcaption>El pie publicado por el medio.</figcaption>
                    </figure>
                    <p>El segundo párrafo también es suficientemente largo como para formar parte de la extracción final del cuerpo de la nota periodística publicada hoy.</p>
                    <p>El tercer párrafo cierra la nota con el texto necesario para que la extracción estática no necesite el navegador headless ni ninguna otra dependencia externa.</p>
                </div>
            </body>
            </html>
            """;

        var service = CreateService(html);

        var content = await service.GetArticleContentAsync(ArticleUrl);

        Assert.NotNull(content);
        Assert.Equal(3, content!.Paragraphs.Count);
        var media = Assert.Single(content.Media);
        Assert.Equal("image", media.Type);
        Assert.Equal("https://www.clarin.com/img/foto.jpg", media.Url);
        Assert.Equal("El pie publicado por el medio.", media.Caption);
        Assert.Equal(1, media.AfterParagraph);
    }

    private static ArticleContentService CreateService(string html)
    {
        var handler = new StaticHtmlHandler(html);
        var news = new NewsAggregationService(
            Mock.Of<IPlaywright>(),
            new HttpClient(handler),
            NullLogger<NewsAggregationService>.Instance,
            Mock.Of<IDailyEditionStore>(),
            new EditionClock(
                new FixedTimeProvider(
                    new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.Zero)),
                Options.Create(new ArchiveOptions())));

        return new ArticleContentService(
            news,
            new HttpClient(handler),
            NullLogger<ArticleContentService>.Instance);
    }

    private sealed class StaticHtmlHandler : HttpMessageHandler
    {
        private readonly string _html;

        public StaticHtmlHandler(string html) => _html = html;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _html,
                    System.Text.Encoding.UTF8,
                    "text/html")
            });
    }
}
