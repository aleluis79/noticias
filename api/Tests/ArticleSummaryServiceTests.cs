using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.Services.Summaries;
using Xunit;

public class ArticleSummaryServiceTests : IDisposable
{
    private const string ArticleUrl = "https://www.clarin.com/nota";

    private const string UsableSummary =
        "El Gobierno anunció un nuevo plan económico que se implementará durante el próximo trimestre. " +
        "La medida busca reducir la inflación y ordenar las cuentas públicas, según explicaron fuentes oficiales. " +
        "La oposición cuestionó el alcance de la iniciativa y pidió más detalles en el Congreso.";

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "noticias-summary-tests",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of the test directory.
        }
    }

    [Fact]
    public async Task GenerateAsync_ShouldStoreAndReuseTheSummaryWithoutCallingTheModelAgain()
    {
        var handler = StubHandler.Success(UsableSummary);
        var service = CreateService(handler);

        var first = await service.GenerateAsync(ArticleUrl);
        var second = await service.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.Ok, first.Status);
        Assert.Equal(UsableSummary, first.Summary?.Summary);
        Assert.Equal(SummaryGenerationStatus.Ok, second.Status);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GenerateAsync_ShouldKeepTheSummaryAcrossServiceInstances()
    {
        var firstService = CreateService(StubHandler.Success(UsableSummary));
        await firstService.GenerateAsync(ArticleUrl);

        var handler = StubHandler.Success("Otro resumen que no debería usarse porque ya está guardado.");
        var secondService = CreateService(handler);

        var stored = await secondService.GetAsync(ArticleUrl);
        var generated = await secondService.GenerateAsync(ArticleUrl);

        Assert.NotNull(stored);
        Assert.Equal(UsableSummary, stored!.Summary);
        Assert.Equal(UsableSummary, generated.Summary?.Summary);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnTheStoredSummaryEvenWhenTheModelChanges()
    {
        var firstService = CreateService(StubHandler.Success(UsableSummary), model: "modelo-viejo");
        await firstService.GenerateAsync(ArticleUrl);

        var handler = StubHandler.Success(UsableSummary);
        var secondService = CreateService(handler, model: "modelo-nuevo");

        var result = await secondService.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.Ok, result.Status);
        Assert.Equal("modelo-viejo", result.Summary?.Model);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnTheStoredSummaryWhenSummariesAreDisabled()
    {
        var firstService = CreateService(StubHandler.Success(UsableSummary));
        await firstService.GenerateAsync(ArticleUrl);

        var handler = StubHandler.Success(UsableSummary);
        var secondService = CreateService(handler, enabled: false);

        var result = await secondService.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.Ok, result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GenerateAsync_ShouldShareOneGenerationBetweenConcurrentRequests()
    {
        var handler = StubHandler.Success(UsableSummary, delayMilliseconds: 50);
        var service = CreateService(handler);

        var results = await Task.WhenAll(
            service.GenerateAsync(ArticleUrl),
            service.GenerateAsync(ArticleUrl));

        Assert.All(results, result => Assert.Equal(SummaryGenerationStatus.Ok, result.Status));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GenerateAsync_ShouldTruncateTheArticleBeforeSendingItToTheModel()
    {
        var handler = StubHandler.Success(UsableSummary);
        var content = new ArticleContent
        {
            Url = ArticleUrl,
            Title = "Una nota larga",
            Paragraphs = new[]
            {
                new string('a', 200),
                "MARCADOR-FINAL-DE-LA-NOTA"
            }
        };
        var contentService = Mock.Of<IArticleContentService>();
        Mock.Get(contentService)
            .Setup(service => service.GetArticleContentAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var service = CreateService(
            handler,
            content: contentService,
            maxInputCharacters: 120);

        await service.GenerateAsync(ArticleUrl);

        var prompt = ReadPrompt(handler.LastBody);
        Assert.NotNull(prompt);
        Assert.Contains("Una nota larga", prompt);
        Assert.Contains("Clarín", prompt);
        Assert.DoesNotContain("MARCADOR-FINAL-DE-LA-NOTA", prompt);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnModelUnavailableWhenOllamaIsUnreachable()
    {
        var handler = StubHandler.Failure(new HttpRequestException("conexión rechazada"));
        var service = CreateService(handler);

        var result = await service.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.ModelUnavailable, result.Status);
        Assert.Null(result.Summary);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnModelUnavailableWhenTheModelIsMissing()
    {
        var handler = StubHandler.Status(HttpStatusCode.NotFound);
        var service = CreateService(handler);

        var result = await service.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.ModelUnavailable, result.Status);
    }

    [Fact]
    public async Task GenerateAsync_ShouldNotStoreAnUnusableResponse()
    {
        var handler = StubHandler.Success("   ");
        var service = CreateService(handler);

        var result = await service.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.GenerationFailed, result.Status);
        Assert.Null(await service.GetAsync(ArticleUrl));
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnContentUnavailableWithoutABody()
    {
        var content = Mock.Of<IArticleContentService>();
        Mock.Get(content)
            .Setup(service => service.GetArticleContentAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArticleContent?)null);

        var handler = StubHandler.Success(UsableSummary);
        var service = CreateService(handler, content: content);

        var result = await service.GenerateAsync(ArticleUrl);

        Assert.Equal(SummaryGenerationStatus.ContentUnavailable, result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task GenerateAsync_ShouldRejectUnsupportedUrls()
    {
        var handler = StubHandler.Success(UsableSummary);
        var service = CreateService(handler);

        var result = await service.GenerateAsync("https://example.com/nota");

        Assert.Equal(SummaryGenerationStatus.InvalidUrl, result.Status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void BuildPrompt_ShouldAskForAShortNeutralSummary()
    {
        var content = new ArticleContent
        {
            Title = "Título de prueba",
            Paragraphs = new[] { "Primer párrafo de la nota." }
        };

        var prompt = ArticleSummaryService.BuildPrompt(content, "Clarín", 12000);

        Assert.Contains("3 o 4 oraciones", prompt);
        Assert.Contains("Título: Título de prueba", prompt);
        Assert.Contains("Medio: Clarín", prompt);
        Assert.Contains("No inventes", prompt);
        Assert.Contains("Primer párrafo de la nota.", prompt);
    }

    [Fact]
    public void CleanSummary_ShouldNormalizeTheModelOutput()
    {
        var summary = ArticleSummaryService.CleanSummary(
            "  Resumen: \"Primera oración del resumen generado. Segunda oración del resumen generado.\"  ");

        Assert.Equal(
            "Primera oración del resumen generado. Segunda oración del resumen generado.",
            summary);
    }

    private ArticleSummaryService CreateService(
        StubHandler handler,
        IArticleContentService? content = null,
        string model = "test-model",
        bool enabled = true,
        int maxInputCharacters = 12000)
    {
        var options = Options.Create(new SummaryOptions
        {
            Enabled = enabled,
            OllamaUrl = "http://ollama.test:11434",
            Model = model,
            Directory = _directory,
            MaxInputCharacters = maxInputCharacters,
            TimeoutSeconds = 5,
            MaxConcurrent = 1
        });

        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };

        return new ArticleSummaryService(
            options,
            content ?? DefaultContent(),
            client,
            NullLogger<ArticleSummaryService>.Instance);
    }

    private static IArticleContentService DefaultContent()
    {
        var content = Mock.Of<IArticleContentService>();
        Mock.Get(content)
            .Setup(service => service.GetArticleContentAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArticleContent
            {
                Url = ArticleUrl,
                Title = "El título de la nota",
                Paragraphs = new[] { new string('p', 400) }
            });

        return content;
    }

    private static string? ReadPrompt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("prompt").GetString();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        private int _calls;

        private StubHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public int Calls => Volatile.Read(ref _calls);

        public string? LastBody { get; private set; }

        public static StubHandler Success(string summary, int delayMilliseconds = 0) =>
            new(async (request, cancellationToken) =>
            {
                if (delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds, cancellationToken);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { response = summary }),
                        Encoding.UTF8,
                        "application/json")
                };
            });

        public static StubHandler Status(HttpStatusCode status) =>
            new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        public static StubHandler Failure(Exception exception) =>
            new((_, _) => Task.FromException<HttpResponseMessage>(exception));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return await _handler(request, cancellationToken);
        }
    }
}
