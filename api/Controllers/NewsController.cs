using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Models;
using NewsAggregator.Services;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly INewsAggregationService _newsService;
    private readonly IArticleContentService _articleContentService;

    public NewsController(
        INewsAggregationService newsService,
        IArticleContentService articleContentService)
    {
        _newsService = newsService;
        _articleContentService = articleContentService;
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
