using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Models;
using NewsAggregator.Services;
using NewsAggregator.Services.Grouping;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly INewsAggregationService _newsService;
    private readonly IArticleContentService _articleContentService;
    private readonly IStoryGroupingService _storyGroupingService;

    public NewsController(
        INewsAggregationService newsService,
        IArticleContentService articleContentService,
        IStoryGroupingService storyGroupingService)
    {
        _newsService = newsService;
        _articleContentService = articleContentService;
        _storyGroupingService = storyGroupingService;
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
    /// publication date.
    /// </summary>
    /// <param name="refresh">Bypass the cache and re-fetch every source.</param>
    [HttpGet("/api/stories")]
    public async Task<ActionResult<StoriesResponse>> GetStories([FromQuery] bool refresh = false)
    {
        try
        {
            var articles = (await _newsService.GetNewsAsync(refresh)).ToList();
            var stories = _storyGroupingService.Group(articles);

            return Ok(new StoriesResponse
            {
                GeneratedAt = DateTime.UtcNow,
                ArticleCount = articles.Count,
                StoryCount = stories.Count,
                Stories = stories
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
}

public class StoriesResponse
{
    public DateTime GeneratedAt { get; set; }
    public int ArticleCount { get; set; }
    public int StoryCount { get; set; }
    public IReadOnlyList<NewsStory> Stories { get; set; } = Array.Empty<NewsStory>();
}
