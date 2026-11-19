using Microsoft.AspNetCore.Mvc;
using NewsAggregator.Models;
using NewsAggregator.Services.Summaries;

namespace NewsAggregator.Controllers
{
    /// <summary>
    /// On-demand summaries of the extracted article body. A summary is generated
    /// once, stored on disk and then always served from the store.
    /// </summary>
    [ApiController]
    [Route("api/news/summary")]
    public class SummariesController : ControllerBase
    {
        private readonly IArticleSummaryService _summaryService;

        public SummariesController(IArticleSummaryService summaryService)
        {
            _summaryService = summaryService;
        }

        /// <summary>Returns the stored summary of an article.</summary>
        [HttpGet]
        public async Task<ActionResult<ArticleSummary>> GetSummary(
            [FromQuery] string url,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url) || !_summaryService.IsSupportedUrl(url))
            {
                return BadRequest("La URL debe pertenecer a una de las fuentes soportadas.");
            }

            try
            {
                var summary = await _summaryService
                    .GetAsync(url, cancellationToken)
                    .ConfigureAwait(false);

                return summary is null
                    ? NotFound("Todavía no hay un resumen para esta nota.")
                    : Ok(summary);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while reading the summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates and stores the summary of an article. If it already exists, the
        /// stored summary is returned and the model is not called.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ArticleSummary>> GenerateSummary(
            [FromQuery] string url,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(url) || !_summaryService.IsSupportedUrl(url))
            {
                return BadRequest("La URL debe pertenecer a una de las fuentes soportadas.");
            }

            try
            {
                var result = await _summaryService
                    .GenerateAsync(url, cancellationToken)
                    .ConfigureAwait(false);

                return result.Status switch
                {
                    SummaryGenerationStatus.Ok when result.Summary is not null =>
                        Ok(result.Summary),
                    SummaryGenerationStatus.InvalidUrl =>
                        BadRequest(result.Error),
                    SummaryGenerationStatus.ContentUnavailable =>
                        NotFound(result.Error ?? "No se pudo extraer el contenido de la nota."),
                    SummaryGenerationStatus.Disabled or SummaryGenerationStatus.ModelUnavailable =>
                        StatusCode(503, result.Error ?? "El servicio de resumen no está disponible."),
                    _ => StatusCode(502, result.Error ?? "No se pudo generar el resumen.")
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return StatusCode(499);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while generating the summary: {ex.Message}");
            }
        }
    }
}
