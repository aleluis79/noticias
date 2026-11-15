using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NewsAggregator.Models;
using NewsAggregator.Services.Parsers;

namespace NewsAggregator.Services
{
    public class NewsAggregationService : INewsAggregationService, IDisposable
    {
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan NavigationTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan NetworkIdleTimeout = TimeSpan.FromSeconds(4);

        private const int MaxParallelFetches = 4;
        private const int MinArticlesBeforeBrowserFallback = 2;

        private static readonly Regex ParagraphTagRegex =
            new(@"<p[\s>]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private sealed record Source(string Url, Func<IParser> ParserFactory);

        private static readonly Source[] Sources =
        {
            new("https://www.clarin.com", () => new ClarínParser()),
            new("https://www.lanacion.com.ar", () => new LaNacionParser()),
            new("https://www.pagina12.com.ar", () => new Pagina12Parser()),
            new("https://tn.com.ar", () => new TNParser()),
            new("https://www.ambito.com", () => new AmbitoFinancieroParser()),
            new("https://www.cronista.com", () => new ElCronistaParser()),
            new("https://www.perfil.com", () => new PerfilParser()),
            new("https://www.c5n.com", () => new C5NParser()),
            new("https://www.lavoz.com.ar", () => new LaVozDelInteriorParser()),
            new("https://www.eldia.com", () => new ElDiaParser()),
            new("https://www.0221.com.ar", () => new VeintiunoParser()),
            new("https://seul.ar", () => new SeulParser())
        };

        /// <summary>
        /// True when the URL points to an article of one of the configured sources.
        /// Used to prevent the content endpoint from fetching arbitrary URLs.
        /// </summary>
        public static bool IsSupportedArticleUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                return false;
            }

            return Sources.Any(source =>
                string.Equals(
                    NormalizeHost(uri.Host),
                    NormalizeHost(new Uri(source.Url).Host),
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeHost(string host) =>
            host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

        /// <summary>
        /// Fetches a single article page. Falls back to the headless browser when the
        /// static HTML has almost no paragraphs (some sites render the body with JS).
        /// </summary>
        public async Task<string?> GetPageHtmlAsync(string url)
        {
            var html = await FetchHtmlAsync(url).ConfigureAwait(false);
            if (html is not null && ParagraphTagRegex.Matches(html).Count >= 3)
            {
                return html;
            }

            var renderedHtml = await FetchWithBrowserAsync(url).ConfigureAwait(false);
            return renderedHtml ?? html;
        }

        private sealed record CacheEntry(DateTime ExpiresAt, IReadOnlyList<NewsArticle> Articles);

        private readonly IPlaywright _playwright;
        private readonly HttpClient _httpClient;
        private readonly ILogger<NewsAggregationService> _logger;

        private readonly SemaphoreSlim _fetchGate = new(MaxParallelFetches, MaxParallelFetches);
        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private readonly SemaphoreSlim _browserGate = new(1, 1);

        private CacheEntry? _cache;
        private IReadOnlyList<NewsArticle>? _lastSnapshot;

        private IBrowser? _browser;
        private IBrowserContext? _browserContext;
        private bool _disposed;

        public NewsAggregationService(
            IPlaywright playwright,
            HttpClient httpClient,
            ILogger<NewsAggregationService> logger)
        {
            _playwright = playwright;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<IEnumerable<NewsArticle>> GetNewsAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && GetCached() is { } cached)
            {
                return cached;
            }

            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!forceRefresh && GetCached() is { } cachedAfterLock)
                {
                    return cachedAfterLock;
                }

                var stopwatch = Stopwatch.StartNew();
                var results = await Task.WhenAll(Sources.Select(FetchSourceAsync)).ConfigureAwait(false);
                var articles = results.SelectMany(articles => articles).ToList();
                stopwatch.Stop();

                if (articles.Count == 0)
                {
                    // Every source failed: keep serving the previous snapshot if we have one.
                    _logger.LogWarning("No articles could be retrieved from any source in {Elapsed} ms", stopwatch.ElapsedMilliseconds);
                    return _lastSnapshot ?? articles;
                }

                _lastSnapshot = articles;
                Volatile.Write(ref _cache, new CacheEntry(DateTime.UtcNow.Add(CacheDuration), articles));

                _logger.LogInformation(
                    "Aggregated {Count} articles from {Sources} sources in {Elapsed} ms",
                    articles.Count, results.Count(result => result.Count > 0), stopwatch.ElapsedMilliseconds);

                return articles;
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        private IReadOnlyList<NewsArticle>? GetCached()
        {
            var entry = Volatile.Read(ref _cache);
            return entry is not null && entry.ExpiresAt > DateTime.UtcNow ? entry.Articles : null;
        }

        private async Task<IReadOnlyList<NewsArticle>> FetchSourceAsync(Source source)
        {
            var parser = source.ParserFactory();
            var stopwatch = Stopwatch.StartNew();

            await _fetchGate.WaitAsync().ConfigureAwait(false);
            try
            {
                IReadOnlyList<NewsArticle> articles = Array.Empty<NewsArticle>();

                var html = await FetchHtmlAsync(source.Url).ConfigureAwait(false);
                if (html is not null)
                {
                    articles = SafeParse(parser, html);
                }

                // Some sites render their content with JavaScript. Only pay for a real
                // browser when the static HTML did not give us enough headlines.
                if (articles.Count < MinArticlesBeforeBrowserFallback)
                {
                    var renderedHtml = await FetchWithBrowserAsync(source.Url).ConfigureAwait(false);
                    if (renderedHtml is not null)
                    {
                        var renderedArticles = SafeParse(parser, renderedHtml);
                        if (renderedArticles.Count > articles.Count)
                        {
                            articles = renderedArticles;
                        }
                    }
                }

                _logger.LogInformation(
                    "{Source}: {Count} articles in {Elapsed} ms",
                    parser.SourceName, articles.Count, stopwatch.ElapsedMilliseconds);

                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch news from {Source}", parser.SourceName);
                return Array.Empty<NewsArticle>();
            }
            finally
            {
                _fetchGate.Release();
            }
        }

        private async Task<string?> FetchHtmlAsync(string url)
        {
            try
            {
                using var timeout = new CancellationTokenSource(HttpTimeout);
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET {Url} returned {StatusCode}", url, (int)response.StatusCode);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                _logger.LogDebug(ex, "HTTP fetch failed for {Url}", url);
                return null;
            }
        }

        private async Task<string?> FetchWithBrowserAsync(string url)
        {
            try
            {
                var context = await GetBrowserContextAsync().ConfigureAwait(false);
                var page = await context.NewPageAsync().ConfigureAwait(false);

                try
                {
                    await page.GotoAsync(url, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = (float)NavigationTimeout.TotalMilliseconds
                    }).ConfigureAwait(false);

                    try
                    {
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                        {
                            Timeout = (float)NetworkIdleTimeout.TotalMilliseconds
                        }).ConfigureAwait(false);
                    }
                    catch (TimeoutException)
                    {
                        // Network never goes fully idle on news sites; the DOM is enough.
                    }

                    return await page.ContentAsync().ConfigureAwait(false);
                }
                finally
                {
                    await page.CloseAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Browser fallback failed for {Url}", url);
                return null;
            }
        }

        private async Task<IBrowserContext> GetBrowserContextAsync()
        {
            if (_browserContext is not null)
            {
                return _browserContext;
            }

            await _browserGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_browserContext is not null)
                {
                    return _browserContext;
                }

                _browser ??= await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true
                }).ConfigureAwait(false);

                _browserContext = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    Locale = "es-AR",
                    UserAgent = UserAgent,
                    IgnoreHTTPSErrors = true
                }).ConfigureAwait(false);

                // Images, media and fonts are irrelevant for text extraction.
                await _browserContext.RouteAsync("**/*", async route =>
                {
                    if (route.Request.ResourceType is "image" or "media" or "font")
                    {
                        await route.AbortAsync().ConfigureAwait(false);
                        return;
                    }

                    await route.ContinueAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);

                return _browserContext;
            }
            finally
            {
                _browserGate.Release();
            }
        }

        private IReadOnlyList<NewsArticle> SafeParse(IParser parser, string html)
        {
            try
            {
                return parser.Parse(html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Parser for {Source} failed", parser.SourceName);
                return Array.Empty<NewsArticle>();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _browserContext?.CloseAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error closing the browser context");
            }

            try
            {
                _browser?.CloseAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error closing the browser");
            }

            _browserContext = null;
            _browser = null;

            _fetchGate.Dispose();
            _refreshGate.Dispose();
            _browserGate.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
