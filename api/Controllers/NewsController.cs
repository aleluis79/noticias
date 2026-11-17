using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.Services.Archive;
using NewsAggregator.Services.Grouping;
using NewsAggregator.Services.Tts;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly INewsAggregationService _newsService;
    private readonly IArticleContentService _articleContentService;
    private readonly IStoryGroupingService _storyGroupingService;
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly IEditionHistoryService _historyService;
    private readonly EditionClock _clock;

    public NewsController(
        INewsAggregationService newsService,
        IArticleContentService articleContentService,
        IStoryGroupingService storyGroupingService,
        ITextToSpeechService textToSpeechService,
        IEditionHistoryService historyService,
        EditionClock clock)
    {
        _newsService = newsService;
        _articleContentService = articleContentService;
        _storyGroupingService = storyGroupingService;
        _textToSpeechService = textToSpeechService;
        _historyService = historyService;
        _clock = clock;
    }

    /// <param name="refresh">Bypass the cache and re-fetch every source.</param>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NewsArticle>>> GetNews([FromQuery] bool refresh = false)
    {
        try
        {
            return Ok(await _newsService.GetNewsAsync(refresh));
        }
        catch (Exception ex)
        {
            // Log the error internally
            return StatusCode(500, $"An error occurred while fetching news: {ex.Message}");
        }
    }

    /// <summary>
    /// Front-page view: articles grouped into the stories they report, each
    /// story ordered by how many outlets cover it. <c>generatedAt</c> is the
    /// only time the front page shows, so it is never mistaken for a
    /// publication date. With <paramref name="date"/> it returns a stored
    /// edition instead of fetching the sources.
    /// </summary>
    /// <param name="refresh">Bypass the cache and re-fetch every source.</param>
    /// <param name="date">Stored edition date in YYYY-MM-DD format.</param>
    [HttpGet("/api/stories")]
    public async Task<ActionResult<StoriesResponse>> GetStories(
        [FromQuery] bool refresh = false,
        [FromQuery] string? date = null,
        CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!TryParseDate(date, out var requested))
            {
                return BadRequest("La fecha debe tener el formato YYYY-MM-DD.");
            }

            if (requested > today)
            {
                return BadRequest("La fecha no puede ser futura.");
            }

            if (requested < today.AddDays(-_historyService.PreviousDaysAvailable))
            {
                return BadRequest("La fecha está fuera del historial disponible.");
            }

            if (requested < today)
            {
                try
                {
                    var stored = await _historyService
                        .GetEditionAsync(requested, cancellationToken)
                        .ConfigureAwait(false);
                    return stored is null
                        ? NotFound("No hay una edición guardada para esa fecha.")
                        : Ok(stored);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"An error occurred while loading the stored edition: {ex.Message}");
                }
            }
        }

        try
        {
            var articles = (await _newsService.GetNewsAsync(refresh)).ToList();
            var stories = _storyGroupingService.Group(articles);
            var previousDays = await _historyService
                .GetPreviousDaySummariesAsync(today, cancellationToken)
                .ConfigureAwait(false);

            return Ok(new StoriesResponse
            {
                Date = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                GeneratedAt = _clock.UtcNow.UtcDateTime,
                ArticleCount = articles.Count,
                StoryCount = stories.Count,
                Stories = stories,
                PreviousDays = previousDays
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while grouping the news: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the full body of an article, extracted on demand from its original URL.
    /// </summary>
    [HttpGet("content")]
    public async Task<ActionResult<ArticleContent>> GetContent(
        [FromQuery] string url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) || !_articleContentService.IsSupportedUrl(url))
        {
            return BadRequest("La URL debe pertenecer a una de las fuentes soportadas.");
        }

        try
        {
            var content = await _articleContentService.GetArticleContentAsync(url, cancellationToken);
            return content is null
                ? NotFound("No se pudo extraer el contenido de la nota.")
                : Ok(content);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while fetching the article: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the locally synthesized audio for one piece of an article:
    /// its title, its attribution or a body paragraph.
    /// </summary>
    [HttpGet("audio")]
    public async Task<IActionResult> GetAudio(
        [FromQuery] string url,
        [FromQuery] string part,
        [FromQuery] int? index,
        [FromQuery] string? title,
        [FromQuery] string? author,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) || !_articleContentService.IsSupportedUrl(url))
        {
            return BadRequest("La URL debe pertenecer a una de las fuentes soportadas.");
        }

        if (!TryParseSegment(part, index, out var segment))
        {
            return BadRequest("El segmento pedido no es válido.");
        }

        if (title is { Length: > 300 } || author is { Length: > 200 })
        {
            return BadRequest("El título o el autor son demasiado largos.");
        }

        if (!_textToSpeechService.IsEnabled)
        {
            return StatusCode(503, "La voz local no está disponible.");
        }

        ArticleContent? article;
        try
        {
            article = await _articleContentService.GetArticleContentAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while fetching the article: {ex.Message}");
        }

        if (article is null)
        {
            return NotFound("No se pudo extraer el contenido de la nota.");
        }

        if (segment.Part == TtsSegmentPart.Body &&
            (segment.Index is null ||
             segment.Index < 0 ||
             segment.Index >= article.Paragraphs.Count))
        {
            return BadRequest("El párrafo pedido no existe en la nota.");
        }

        var sourceName = NewsAggregationService.GetSourceNameForUrl(url);
        var text = TtsTextBuilder.Build(segment, article, sourceName, title, author);
        if (string.IsNullOrWhiteSpace(text))
        {
            return NotFound("No hay texto para sintetizar en ese segmento.");
        }

        var scopeParts = segment.Part switch
        {
            TtsSegmentPart.Title => $"{url.Trim()}|title",
            TtsSegmentPart.Attribution => $"{url.Trim()}|attribution",
            _ => $"{url.Trim()}|body|{segment.Index}"
        };
        var scope = $"{scopeParts}|{text}";

        TtsAudio? audio;
        try
        {
            audio = await _textToSpeechService.SynthesizeAsync(scope, text, cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while synthesizing the audio: {ex.Message}");
        }

        if (audio is null)
        {
            return StatusCode(503, "La voz local no está disponible.");
        }

        Response.Headers.CacheControl = "public, max-age=86400";
        return File(audio.Bytes, audio.ContentType, enableRangeProcessing: true);
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static bool TryParseSegment(string? part, int? index, out TtsSegment segment)
    {
        switch (part?.Trim().ToLowerInvariant())
        {
            case "title":
                segment = new TtsSegment(TtsSegmentPart.Title, null);
                return true;
            case "attribution":
                segment = new TtsSegment(TtsSegmentPart.Attribution, null);
                return true;
            case "body" when index is >= 0:
                segment = new TtsSegment(TtsSegmentPart.Body, index);
                return true;
            default:
                segment = new TtsSegment(TtsSegmentPart.Body, null);
                return false;
        }
    }
}
