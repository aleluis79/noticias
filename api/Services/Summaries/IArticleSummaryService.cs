using NewsAggregator.Models;

namespace NewsAggregator.Services.Summaries
{
    public enum SummaryGenerationStatus
    {
        Ok,
        InvalidUrl,
        ContentUnavailable,
        Disabled,
        ModelUnavailable,
        GenerationFailed
    }

    public sealed record SummaryGenerationResult(
        SummaryGenerationStatus Status,
        ArticleSummary? Summary,
        string? Error);

    public interface IArticleSummaryService
    {
        /// <summary>True when the URL belongs to one of the supported news sources.</summary>
        bool IsSupportedUrl(string? url);

        /// <summary>
        /// Returns the stored summary for an article, or null when it was not
        /// generated yet. Never calls the language model.
        /// </summary>
        Task<ArticleSummary?> GetAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the stored summary or generates and stores it. Never regenerates
        /// an existing summary.
        /// </summary>
        Task<SummaryGenerationResult> GenerateAsync(string url, CancellationToken cancellationToken = default);
    }
}
