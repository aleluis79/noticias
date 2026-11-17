using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NewsAggregator.Models;

namespace NewsAggregator.Services.Archive
{
    /// <summary>
    /// Stores one JSON file per edition date. Writes are atomic, reads are
    /// cached in memory, and an unreadable file is treated as a missing day.
    /// </summary>
    public sealed class DailyEditionStore : IDailyEditionStore
    {
        private const string DateFormat = "yyyy-MM-dd";

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web);

        private readonly ArchiveOptions _options;
        private readonly string _directory;
        private readonly ILogger<DailyEditionStore> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly Dictionary<DateOnly, StoredEdition> _cache = new();

        public DailyEditionStore(
            IOptions<ArchiveOptions> options,
            ILogger<DailyEditionStore> logger)
        {
            _options = options.Value;
            _directory = _options.Directory;
            _logger = logger;
        }

        public async Task SaveAsync(
            DateOnly date,
            IReadOnlyList<NewsArticle> articles,
            DateTime capturedAtUtc,
            CancellationToken cancellationToken = default)
        {
            if (articles.Count == 0)
            {
                return;
            }

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(_directory);

                var edition = new StoredEdition
                {
                    Date = date,
                    CapturedAt = capturedAtUtc.Kind == DateTimeKind.Utc
                        ? capturedAtUtc
                        : capturedAtUtc.ToUniversalTime(),
                    Articles = articles.ToList()
                };

                var json = JsonSerializer.Serialize(edition, JsonOptions);
                var path = GetPath(date);
                var temporaryPath = $"{path}.tmp-{Guid.NewGuid():N}";

                await File.WriteAllTextAsync(temporaryPath, json, cancellationToken)
                    .ConfigureAwait(false);
                File.Move(temporaryPath, path, overwrite: true);

                _cache[date] = edition;
                PurgeOlderThan(date);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not store the edition for {Date}", date);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<StoredEdition?> GetAsync(
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

                var path = GetPath(date);
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(path, cancellationToken)
                    .ConfigureAwait(false);
                var stored = JsonSerializer.Deserialize<StoredEdition>(json, JsonOptions);
                if (stored is null)
                {
                    return null;
                }

                // The file name is the source of truth for the date.
                stored.Date = date;
                _cache[date] = stored;
                return stored;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not read the edition for {Date}", date);
                return null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<StoredEdition>> GetPreviousAsync(
            DateOnly today,
            int count,
            CancellationToken cancellationToken = default)
        {
            var editions = new List<StoredEdition>(Math.Max(0, count));
            for (var daysAgo = 1; daysAgo <= count; daysAgo++)
            {
                var edition = await GetAsync(today.AddDays(-daysAgo), cancellationToken)
                    .ConfigureAwait(false);
                if (edition is not null)
                {
                    editions.Add(edition);
                }
            }

            return editions;
        }

        private void PurgeOlderThan(DateOnly date)
        {
            var oldestDate = date.AddDays(-(_options.RetentionDays - 1));

            foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!DateOnly.TryParseExact(
                        name,
                        DateFormat,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var fileDate) ||
                    fileDate >= oldestDate)
                {
                    continue;
                }

                try
                {
                    File.Delete(path);
                    _cache.Remove(fileDate);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not delete the old edition {Date}", fileDate);
                }
            }

            foreach (var path in Directory.EnumerateFiles(_directory, "*.tmp-*"))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogDebug(ex, "Could not delete temporary archive file {Path}", path);
                }
            }
        }

        private string GetPath(DateOnly date) =>
            Path.Combine(_directory, $"{date.ToString(DateFormat, CultureInfo.InvariantCulture)}.json");
    }
}
