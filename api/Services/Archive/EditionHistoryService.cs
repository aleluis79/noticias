using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAggregator.Models;
using NewsAggregator.Services.Grouping;

namespace NewsAggregator.Services.Archive
{
    public sealed class EditionHistoryService : IEditionHistoryService
    {
        private readonly IDailyEditionStore _store;
        private readonly IStoryGroupingService _grouping;
        private readonly ILogger<EditionHistoryService> _logger;
        private readonly ArchiveOptions _options;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<DateOnly, StoriesResponse> _cache = new();

        public EditionHistoryService(
            IDailyEditionStore store,
            IStoryGroupingService grouping,
            IOptions<ArchiveOptions> options,
            ILogger<EditionHistoryService> logger)
        {
            _store = store;
            _grouping = grouping;
            _options = options.Value;
            _logger = logger;
        }

        public int PreviousDaysAvailable => _options.PreviousDays;

        public async Task<IReadOnlyList<NewsDaySummary>> GetPreviousDaySummariesAsync(
            DateOnly today,
            CancellationToken cancellationToken = default)
        {
            var summaries = new List<NewsDaySummary>(PreviousDaysAvailable);

            for (var daysAgo = 1; daysAgo <= PreviousDaysAvailable; daysAgo++)
            {
                var edition = await GetEditionAsync(today.AddDays(-daysAgo), cancellationToken)
                    .ConfigureAwait(false);
                if (edition is null)
                {
                    continue;
                }

                summaries.Add(new NewsDaySummary
                {
                    Date = edition.Date,
                    ArticleCount = edition.ArticleCount,
                    StoryCount = edition.StoryCount
                });
            }

            return summaries;
        }

        public async Task<StoriesResponse?> GetEditionAsync(
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(date, out var cached))
                {
                    return cached;
                }

                var stored = await _store.GetAsync(date, cancellationToken).ConfigureAwait(false);
                if (stored is null)
                {
                    return null;
                }

                var stories = _grouping.Group(stored.Articles);
                var response = new StoriesResponse
                {
                    Date = FormatDate(date),
                    GeneratedAt = stored.CapturedAt,
                    ArticleCount = stored.Articles.Count,
                    StoryCount = stories.Count,
                    Stories = stories,
                    PreviousDays = Array.Empty<NewsDaySummary>()
                };

                _cache[date] = response;
                _logger.LogInformation(
                    "Loaded stored edition {Date} with {Articles} articles and {Stories} stories",
                    response.Date,
                    response.ArticleCount,
                    response.StoryCount);

                return response;
            }
            finally
            {
                _gate.Release();
            }
        }

        private static string FormatDate(DateOnly date) =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
