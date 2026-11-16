using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NewsAggregator.Models;

namespace NewsAggregator.Services.Grouping
{
    /// <summary>
    /// Groups headlines that report the same story so the front page can show
    /// one story with several outlets instead of repeating it once per outlet.
    ///
    /// The rule is deliberately conservative: two headlines only join when
    /// their significant words overlap enough, measured by
    /// <see cref="HeadlineSimilarity"/>. When in doubt, they stay apart — a
    /// wrong merge would claim that an outlet covered something it did not.
    /// </summary>
    public class StoryGroupingService : IStoryGroupingService
    {
        /// <summary>
        /// Measured against the real feeds. At 0.55 the day's biggest story
        /// stayed split into near-duplicate groups; at 0.50 the clustered
        /// volume roughly doubles with no false merges measured, and the
        /// Pope-hit / Pope-farewell pair (0.31) stays apart. Going lower
        /// starts merging different angles on the same subject.
        /// </summary>
        public const double SameStoryThreshold = 0.50;

        /// <summary>Twelve is the number of configured outlets.</summary>
        public const int MaxArticlesPerStory = 12;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private readonly ILogger<StoryGroupingService> _logger;
        private readonly double _threshold;

        private IReadOnlyList<NewsArticle>? _cachedInput;
        private IReadOnlyList<NewsStory>? _cachedResult;
        private DateTime _cachedAt;

        public StoryGroupingService(
            ILogger<StoryGroupingService> logger,
            double threshold = SameStoryThreshold)
        {
            _logger = logger;
            _threshold = threshold;
        }

        public IReadOnlyList<NewsStory> Group(IReadOnlyList<NewsArticle> articles)
        {
            if (ReferenceEquals(articles, _cachedInput) &&
                _cachedResult is not null &&
                DateTime.UtcNow - _cachedAt < CacheDuration)
            {
                return _cachedResult;
            }

            var stopwatch = Stopwatch.StartNew();
            var stories = BuildStories(articles);
            stopwatch.Stop();

            _cachedInput = articles;
            _cachedResult = stories;
            _cachedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Grouped {Articles} articles into {Stories} stories ({Clustered} clustered) in {Elapsed} ms",
                articles.Count,
                stories.Count,
                stories.Count(story => story.SourceCount > 1),
                stopwatch.ElapsedMilliseconds);

            return stories;
        }

        private IReadOnlyList<NewsStory> BuildStories(IReadOnlyList<NewsArticle> articles)
        {
            if (articles.Count == 0)
            {
                return Array.Empty<NewsStory>();
            }

            var words = new IReadOnlySet<string>[articles.Count];
            for (var i = 0; i < articles.Count; i++)
            {
                words[i] = HeadlineSimilarity.SignificantWords(articles[i].Title);
            }

            var parent = new int[articles.Count];
            var size = new int[articles.Count];
            for (var i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
                size[i] = 1;
            }

            for (var i = 0; i < articles.Count; i++)
            {
                for (var j = i + 1; j < articles.Count; j++)
                {
                    var rootLeft = Find(parent, i);
                    var rootRight = Find(parent, j);
                    if (rootLeft == rootRight)
                    {
                        continue;
                    }

                    if (size[rootLeft] + size[rootRight] > MaxArticlesPerStory)
                    {
                        continue;
                    }

                    if (HeadlineSimilarity.TokenSetRatio(words[i], words[j]) < _threshold)
                    {
                        continue;
                    }

                    // Union by size keeps the trees shallow and the order stable.
                    if (size[rootLeft] < size[rootRight])
                    {
                        (rootLeft, rootRight) = (rootRight, rootLeft);
                    }

                    parent[rootRight] = rootLeft;
                    size[rootLeft] += size[rootRight];
                }
            }

            var groups = new Dictionary<int, List<int>>();
            for (var i = 0; i < articles.Count; i++)
            {
                var root = Find(parent, i);
                if (!groups.TryGetValue(root, out var group))
                {
                    group = new List<int>(4);
                    groups[root] = group;
                }

                group.Add(i);
            }

            var stories = new List<NewsStory>(groups.Count);
            foreach (var group in groups.Values)
            {
                stories.Add(BuildStory(group, articles));
            }

            // Most-covered story first, then the freshest real date, then the
            // order the outlets were fetched in.
            stories.Sort((left, right) =>
            {
                var bySources = right.SourceCount.CompareTo(left.SourceCount);
                if (bySources != 0)
                {
                    return bySources;
                }

                var byDate = Nullable.Compare(right.LatestPublishedDate, left.LatestPublishedDate);
                return byDate != 0 ? byDate : 0;
            });

            return stories;
        }

        private static NewsStory BuildStory(List<int> group, IReadOnlyList<NewsArticle> articles)
        {
            // Groups are built from an ordered scan, so the leading article is
            // the first one in the configured source order.
            var lead = articles[group[0]];

            var sources = group
                .Select(index => articles[index])
                .Select(article => new NewsStorySource
                {
                    SourceName = article.SourceName,
                    Title = article.Title,
                    Summary = article.Summary,
                    OriginalUrl = article.OriginalUrl,
                    PublishedDate = article.PublishedDate
                })
                .ToList();

            var dates = sources
                .Where(source => source.PublishedDate.HasValue)
                .Select(source => source.PublishedDate!.Value)
                .ToList();

            return new NewsStory
            {
                Title = lead.Title,
                Summary = lead.Summary,
                OriginalUrl = lead.OriginalUrl,
                SourceName = lead.SourceName,
                SourceCount = sources.Select(source => source.SourceName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ArticleCount = sources.Count,
                LatestPublishedDate = dates.Count > 0 ? dates.Max() : null,
                Sources = sources
            };
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }
    }
}
