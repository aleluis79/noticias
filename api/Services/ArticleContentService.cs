using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using NewsAggregator.Models;

namespace NewsAggregator.Services
{
    /// <summary>
    /// Downloads news article pages on demand and extracts their body as plain-text
    /// paragraphs. Results are cached per URL so expanding the same note twice does
    /// not hit the source site again.
    /// </summary>
    public class ArticleContentService : IArticleContentService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan FeedCacheDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan FeedTimeout = TimeSpan.FromSeconds(15);

        private const int MinParagraphLength = 40;
        private const int MinBodyCharacters = 300;
        private const int MaxParagraphs = 200;
        private const int MaxCharacters = 30000;
        private const int MaxFeedPages = 6;

        private const string SeulHost = "seul.ar";

        /// <summary>
        /// WordPress appends the post title to the RSS body. It is not part of the note.
        /// </summary>
        private static readonly Regex FeedTrailerRegex = new(
            @"\s*La entrada .*? se public[oó] primero en .*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// The registration wall that Seúl inserts into some articles. The stylesheet
        /// and scripts carry "seul-paywall" on every page, so the widget itself is what
        /// identifies a gated note.
        /// </summary>
        private static readonly string[] SeulPaywallMarkers =
        {
            "data-seul-widget",
            "seul-paywall__titulo",
            "Seguí leyendo. Es gratis.",
            "Creá tu cuenta y accedé a todos los artículos de Seúl."
        };

        /// <summary>Containers that news sites usually wrap the body of a note in.</summary>
        private static readonly string[] BodySelectors =
        {
            "[itemprop='articleBody']",
            "[data-testid='article-body']",
            "[data-testid='articleBody']",
            ".article-body", ".article__body", ".article-body-content",
            ".article-content", ".article__content", ".article-text",
            ".articletext", ".articleBody", ".nota-cuerpo", ".nota__cuerpo",
            ".nota-body", ".body-nota", ".body-article", ".cuerpo-nota", ".story-body",
            ".story-body__inner", ".entry-content", ".post-content", ".post-body",
            ".content-body", ".richtext", ".cuerpo__nota", ".cuerpo",
            ".elementor-widget-theme-post-content",
            "#article-body", "#articleBody", "#nota-cuerpo", "#cuerpo-nota",
            "#cuerpo", "#cuerpo__nota"
        };

        /// <summary>Class or id tokens that identify boilerplate around the article.</summary>
        private static readonly string[] NoiseTokens =
        {
            "newsletter", "suscribite", "suscripcion", "subscription", "suscribirse",
            "comentarios", "comentario", "comments", "comment",
            "relacionadas", "relacionados", "related",
            "compartir", "comparti", "share", "sharing", "social",
            "publicidad", "advertising", "advert", "banner", "sponsor",
            "cookie", "cookies", "consent",
            "breadcrumb", "breadcrumbs", "tags", "taglist", "etiquetas",
            "autor", "author", "byline",
            "footer", "sidebar", "widget", "menu", "nav", "navigation",
            "modal", "popup", "login", "registro", "encuesta"
        };

        /// <summary>Meta tags that news sites use to publish the author of a note.</summary>
        private static readonly string[] AuthorMetaSelectors =
        {
            "meta[name='author']",
            "meta[property='article:author']",
            "meta[property='og:article:author']",
            "meta[name='parsely-author']",
            "meta[name='sailthru.author']",
            "meta[name='dc.creator']",
            "meta[name='DC.creator']",
            "meta[name='twitter:creator']"
        };

        /// <summary>Elements that news sites usually wrap the author byline in.</summary>
        private static readonly string[] AuthorSelectors =
        {
            "[itemprop='author'] [itemprop='name']",
            "[itemprop='author']",
            "[rel='author']",
            ".article-author__name",
            ".author-name",
            ".byline__name",
            ".article__author",
            ".article-author",
            ".nota__autor",
            ".nota-autor",
            ".autor__firmante",
            ".autor",
            ".firma",
            ".byline",
            ".story-author",
            ".post-author",
            ".entry-author",
            ".news-headline__author-name",
            ".news-author-and-share__author-name"
        };

        /// <summary>Bylines that identify the newsroom instead of a person.</summary>
        private static readonly string[] GenericAuthorValues =
        {
            "redaccion", "staff", "newsroom", "agencia", "agencias", "editorial"
        };

        private static readonly char[] AuthorSeparators = { '|', '•', '\n', '\r' };

        private static readonly char[] TokenSeparators =
        {
            ' ', '-', '_', ':', '/', '.', '[', ']', '(', ')', ',', '#'
        };

        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        private static readonly Regex DiacriticsRegex = new(@"\p{Mn}+", RegexOptions.Compiled);

        private static readonly Regex BoilerplateRegex = new(
            @"^(le[ée] tambi[ée]n|te puede interesar|notas? relacionadas?|segu[íi] leyendo|seguir leyendo|suscrib|coment[áa]|compart[íi]|publicidad|foto|imagen|video|cr[ée]dito|todos los derechos|©|copyright|t[ée]rminos|pol[íi]tica de privacidad|recib[íi]|sumate|descarg[áa]|escuch[áa]|mir[áa]|entr[áa]|hac[ée] clic|clic aqu[íi])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly NewsAggregationService _pageFetcher;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ArticleContentService> _logger;

        private readonly ConcurrentDictionary<string, CacheEntry> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, FeedCacheEntry> _feedCache =
            new(StringComparer.OrdinalIgnoreCase);

        public ArticleContentService(
            NewsAggregationService pageFetcher,
            HttpClient httpClient,
            ILogger<ArticleContentService> logger)
        {
            _pageFetcher = pageFetcher;
            _httpClient = httpClient;
            _logger = logger;
        }

        private sealed record CacheEntry(DateTime ExpiresAt, ArticleContent? Content);

        private sealed record FeedCacheEntry(DateTime ExpiresAt, string Xml);

        public bool IsSupportedUrl(string? url) =>
            NewsAggregationService.IsSupportedArticleUrl(url);

        public async Task<ArticleContent?> GetArticleContentAsync(
            string url,
            CancellationToken cancellationToken = default)
        {
            if (!IsSupportedUrl(url))
            {
                return null;
            }

            var cacheKey = url.Trim();
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                if (cached.ExpiresAt > DateTime.UtcNow)
                {
                    return cached.Content;
                }

                _cache.TryRemove(cacheKey, out _);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var html = await _pageFetcher.GetPageHtmlAsync(cacheKey).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var walled = IsSeulRegistrationWall(html);
            var paragraphs = html is null
                ? Array.Empty<string>()
                : walled
                    ? await TryGetSeulFeedParagraphsAsync(cacheKey, html, cancellationToken)
                        .ConfigureAwait(false)
                    : ExtractParagraphs(html);

            var content = html is null || (paragraphs.Count == 0 && !walled)
                ? null
                : new ArticleContent
                {
                    Url = cacheKey,
                    Title = ExtractTitle(html!) ?? string.Empty,
                    Author = ExtractAuthor(html!) ?? string.Empty,
                    Paragraphs = paragraphs,
                    RequiresAccount = walled && paragraphs.Count == 0
                };

            _cache[cacheKey] = new CacheEntry(
                DateTime.UtcNow.Add(
                    content is null || content.RequiresAccount
                        ? FailureCacheDuration
                        : CacheDuration),
                content);

            if (content?.RequiresAccount == true)
            {
                _logger.LogInformation(
                    "{Url} is behind Seúl's free registration wall and was not found in its RSS feed",
                    cacheKey);
            }
            else if (content is null)
            {
                _logger.LogInformation("No article body could be extracted from {Url}", cacheKey);
            }

            return content;
        }

        /// <summary>
        /// True when Seúl served its registration wall instead of the article body.
        /// Visible for tests.
        /// </summary>
        internal static bool IsSeulRegistrationWall(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return false;
            }

            return SeulPaywallMarkers.Any(marker =>
                html.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Seúl's public RSS still publishes the full body of the notes it gates on the
        /// website. When the article page only has the registration wall, look for the
        /// same URL in the monthly feed (and then the main feed). Visible for tests.
        /// </summary>
        private async Task<IReadOnlyList<string>> TryGetSeulFeedParagraphsAsync(
            string articleUrl,
            string articleHtml,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(articleUrl, UriKind.Absolute, out var articleUri) ||
                !HostMatches(articleUri.Host, SeulHost))
            {
                return Array.Empty<string>();
            }

            var published = ExtractPublishedDate(articleHtml);

            foreach (var feedUrl in BuildFeedUrls(articleUri, published))
            {
                var paragraphs = await SearchFeedAsync(
                        feedUrl,
                        articleUrl,
                        published,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (paragraphs.Count > 0)
                {
                    _logger.LogInformation(
                        "Recovered the body of {Url} from Seúl's RSS",
                        articleUrl);
                    return paragraphs;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            return Array.Empty<string>();
        }

        private static IEnumerable<string> BuildFeedUrls(Uri articleUri, DateTime? published)
        {
            var root = articleUri.GetLeftPart(UriPartial.Authority);

            if (published is { } date)
            {
                yield return $"{root}/{date:yyyy}/{date:MM}/feed/";
            }

            yield return $"{root}/feed/";
        }

        /// <summary>
        /// Walks the feed pages until the article appears or the feed moves past its
        /// publication date. Monthly feeds have at most a handful of pages.
        /// </summary>
        private async Task<IReadOnlyList<string>> SearchFeedAsync(
            string feedUrl,
            string articleUrl,
            DateTime? published,
            CancellationToken cancellationToken)
        {
            for (var page = 1; page <= MaxFeedPages; page++)
            {
                var pageUrl = page == 1 ? feedUrl : $"{feedUrl}?paged={page}";
                var xml = await GetFeedXmlAsync(pageUrl, cancellationToken).ConfigureAwait(false);
                if (xml is null)
                {
                    return Array.Empty<string>();
                }

                var items = ParseFeedItems(xml);
                if (items.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var match = FindMatchingFeedItem(items, articleUrl);
                if (match is not null)
                {
                    var paragraphs = ExtractFeedParagraphs(match);
                    if (paragraphs.Count > 0)
                    {
                        return paragraphs;
                    }
                }

                DateTime? oldest = null;
                foreach (var item in items)
                {
                    if (item.Published is { } date && (oldest is null || date < oldest))
                    {
                        oldest = date;
                    }
                }

                // The feed is ordered newest first: once it is older than the note,
                // the note is not in the feed (or the slug no longer matches).
                if (published is { } target &&
                    oldest is { } oldestDate &&
                    oldestDate.Date < target.Date.AddDays(-1))
                {
                    return Array.Empty<string>();
                }
            }

            return Array.Empty<string>();
        }

        private async Task<string?> GetFeedXmlAsync(
            string url,
            CancellationToken cancellationToken)
        {
            if (_feedCache.TryGetValue(url, out var cached))
            {
                if (cached.ExpiresAt > DateTime.UtcNow)
                {
                    return cached.Xml;
                }

                _feedCache.TryRemove(url, out _);
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(FeedTimeout);

                using var response = await _httpClient
                    .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug(
                        "GET {Url} returned {StatusCode} while looking for a Seúl article",
                        url,
                        (int)response.StatusCode);
                    return null;
                }

                var xml = await response.Content
                    .ReadAsStringAsync(timeout.Token)
                    .ConfigureAwait(false);

                _feedCache[url] = new FeedCacheEntry(
                    DateTime.UtcNow.Add(FeedCacheDuration),
                    xml);

                return xml;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or OperationCanceledException)
            {
                _logger.LogDebug(exception, "Seúl feed fetch failed for {Url}", url);
                return null;
            }
        }

        private static IReadOnlyList<FeedItem> ParseFeedItems(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return Array.Empty<FeedItem>();
            }

            XDocument document;
            try
            {
                document = XDocument.Parse(xml, LoadOptions.None);
            }
            catch (Exception exception) when (exception is System.Xml.XmlException or ArgumentException)
            {
                return Array.Empty<FeedItem>();
            }

            XNamespace content = "http://purl.org/rss/1.0/modules/content/";

            return document
                .Descendants("item")
                .Select(item => new FeedItem(
                    (string?)item.Element("link"),
                    (string?)item.Element(content + "encoded")
                        ?? (string?)item.Element("description"),
                    ParseFeedDate((string?)item.Element("pubDate"))))
                .ToList();
        }

        private static DateTime? ParseFeedDate(string? value)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var date)
                ? date
                : null;
        }

        private static FeedItem? FindMatchingFeedItem(
            IReadOnlyList<FeedItem> items,
            string articleUrl) =>
            items.FirstOrDefault(item => UrlsMatch(item.Link, articleUrl));

        /// <summary>
        /// Extracts the body of one article from a single RSS page. Visible for tests.
        /// </summary>
        internal static IReadOnlyList<string> ExtractFeedParagraphs(
            string feedXml,
            string articleUrl)
        {
            var match = FindMatchingFeedItem(ParseFeedItems(feedXml), articleUrl);
            return match is null
                ? Array.Empty<string>()
                : ExtractFeedParagraphs(match);
        }

        private static IReadOnlyList<string> ExtractFeedParagraphs(FeedItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ContentHtml) ||
                IsSeulRegistrationWall(item.ContentHtml))
            {
                return Array.Empty<string>();
            }

            var paragraphs = ExtractParagraphs(item.ContentHtml);

            return paragraphs
                .Select(paragraph => FeedTrailerRegex.Replace(paragraph, string.Empty).Trim())
                .Where(paragraph => paragraph.Length >= MinParagraphLength)
                .ToList();
        }

        private static bool UrlsMatch(string? candidate, string articleUrl)
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var candidateUri) ||
                !Uri.TryCreate(articleUrl, UriKind.Absolute, out var articleUri))
            {
                return false;
            }

            return HostMatches(candidateUri.Host, articleUri.Host) &&
                   string.Equals(
                       candidateUri.AbsolutePath.TrimEnd('/'),
                       articleUri.AbsolutePath.TrimEnd('/'),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool HostMatches(string host, string otherHost) =>
            string.Equals(
                TrimWww(host),
                TrimWww(otherHost),
                StringComparison.OrdinalIgnoreCase);

        private static string TrimWww(string host) =>
            host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

        /// <summary>
        /// Reads the publication date from metadata or structured data. The monthly
        /// RSS archive is built from it. Visible for tests.
        /// </summary>
        internal static DateTime? ExtractPublishedDate(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var document = new HtmlParser().ParseDocument(html);

            foreach (var selector in new[]
                     {
                         "meta[property='article:published_time']",
                         "meta[name='article:published_time']",
                         "meta[itemprop='datePublished']",
                         "time[datetime]"
                     })
            {
                var element = document.QuerySelector(selector);
                var value = element?.GetAttribute("content")
                            ?? element?.GetAttribute("datetime");

                if (TryParsePublishedDate(value, out var date))
                {
                    return date;
                }
            }

            foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
            {
                JsonDocument json;
                try
                {
                    json = JsonDocument.Parse(script.TextContent);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (json)
                {
                    foreach (var node in EnumerateJsonLdNodes(json.RootElement))
                    {
                        if (node.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var raw = GetString(node, "datePublished")
                                  ?? GetString(node, "dateCreated");

                        if (TryParsePublishedDate(raw, out var date))
                        {
                            return date;
                        }
                    }
                }
            }

            return null;
        }

        private static bool TryParsePublishedDate(string? value, out DateTime date)
        {
            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var parsed))
            {
                date = parsed.DateTime;
                return true;
            }

            date = default;
            return false;
        }

        private sealed record FeedItem(
            string? Link,
            string? ContentHtml,
            DateTime? Published);

        /// <summary>
        /// Extracts the author byline from structured data, meta tags or the visible
        /// byline. Returns null when the source does not publish one. Visible for tests.
        /// </summary>
        internal static string? ExtractAuthor(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var document = new HtmlParser().ParseDocument(html);

            return ExtractJsonLdAuthor(document)
                   ?? ExtractMetaAuthor(document)
                   ?? ExtractBylineAuthor(document);
        }

        /// <summary>
        /// Extracts the page title from structured metadata or the visible heading.
        /// Returns null when the page does not publish a usable title. Visible for tests.
        /// </summary>
        internal static string? ExtractTitle(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var document = new HtmlParser().ParseDocument(html);

            foreach (var selector in new[]
                     {
                         "meta[property='og:title']",
                         "meta[name='twitter:title']"
                     })
            {
                var value = CleanTitle(document.QuerySelector(selector)?.GetAttribute("content"));
                if (value is not null)
                {
                    return value;
                }
            }

            return CleanTitle(document.QuerySelector("h1")?.TextContent)
                   ?? CleanTitle(document.Title);
        }

        private static string? CleanTitle(string? value)
        {
            var cleaned = Clean(value);
            return cleaned is { Length: >= 3 and <= 300 } ? cleaned : null;
        }

        private static string? ExtractJsonLdAuthor(IDocument document)
        {
            foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
            {
                JsonDocument json;
                try
                {
                    json = JsonDocument.Parse(script.TextContent);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (json)
                {
                    foreach (var node in EnumerateJsonLdNodes(json.RootElement))
                    {
                        if (node.ValueKind != JsonValueKind.Object ||
                            !node.TryGetProperty("author", out var author))
                        {
                            continue;
                        }

                        var name = ReadAuthorName(author);
                        if (name is not null)
                        {
                            return name;
                        }
                    }
                }
            }

            return null;
        }

        private static string? ReadAuthorName(JsonElement author)
        {
            switch (author.ValueKind)
            {
                case JsonValueKind.String:
                    return CleanAuthor(author.GetString());

                case JsonValueKind.Object:
                    return author.TryGetProperty("name", out var name) &&
                           name.ValueKind == JsonValueKind.String
                        ? CleanAuthor(name.GetString())
                        : null;

                case JsonValueKind.Array:
                    var names = author
                        .EnumerateArray()
                        .Select(ReadAuthorName)
                        .Where(name => name is not null)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    return names.Count > 0 ? string.Join(", ", names) : null;

                default:
                    return null;
            }
        }

        private static string? ExtractMetaAuthor(IDocument document)
        {
            foreach (var selector in AuthorMetaSelectors)
            {
                foreach (var meta in document.QuerySelectorAll(selector))
                {
                    var name = CleanAuthor(meta.GetAttribute("content"));
                    if (name is not null)
                    {
                        return name;
                    }
                }
            }

            return null;
        }

        private static string? ExtractBylineAuthor(IDocument document)
        {
            string? best = null;

            foreach (var selector in AuthorSelectors)
            {
                foreach (var element in document.QuerySelectorAll(selector))
                {
                    var candidate = CleanAuthor(element.TextContent);
                    if (candidate is null)
                    {
                        continue;
                    }

                    // Wrappers often include the name plus the author's beat or social
                    // links; the shortest plausible text is usually the name itself.
                    if (best is null || candidate.Length < best.Length)
                    {
                        best = candidate;
                    }
                }
            }

            return best;
        }

        private static string? CleanAuthor(string? value)
        {
            var cleaned = Clean(value);
            if (cleaned is null)
            {
                return null;
            }

            // Some bylines add the date or the section after a separator.
            cleaned = cleaned
                .Split(AuthorSeparators, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?
                .Trim();

            if (string.IsNullOrEmpty(cleaned))
            {
                return null;
            }

            if (cleaned.StartsWith("por ", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[4..].Trim();
            }

            // article:author meta tags sometimes point to a profile URL.
            if (cleaned.Contains("://", StringComparison.Ordinal))
            {
                return null;
            }

            return IsPlausibleAuthor(cleaned) ? cleaned : null;
        }

        private static bool IsPlausibleAuthor(string value)
        {
            if (value.Length is < 3 or > 100 || char.IsDigit(value[0]))
            {
                return false;
            }

            var normalized = NormalizeAuthor(value);

            if (GenericAuthorValues.Any(generic =>
                    normalized == generic ||
                    normalized.StartsWith(generic + " ", StringComparison.Ordinal)))
            {
                return false;
            }

            return !normalized.Contains("min de lectura", StringComparison.Ordinal) &&
                   !normalized.Contains("minutos de lectura", StringComparison.Ordinal);
        }

        private static string NormalizeAuthor(string value) =>
            DiacriticsRegex
                .Replace(value.Normalize(NormalizationForm.FormD), string.Empty)
                .ToLowerInvariant();

        /// <summary>
        /// Extracts the article body from a full page. Visible for tests.
        /// </summary>
        internal static IReadOnlyList<string> ExtractParagraphs(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return Array.Empty<string>();
            }

            var document = new HtmlParser().ParseDocument(html);

            // Read structured data before RemoveNoise() drops every <script> tag.
            var jsonLdParagraphs = ExtractJsonLdParagraphs(document);
            RemoveNoise(document);

            var bodyParagraphs = BestParagraphs(document, BodySelectors);
            if (IsBodyEnough(bodyParagraphs))
            {
                return Limit(bodyParagraphs);
            }

            if (IsBodyEnough(jsonLdParagraphs))
            {
                return Limit(jsonLdParagraphs);
            }

            var articleParagraphs = BestParagraphs(document, new[] { "article" });
            if (IsBodyEnough(articleParagraphs))
            {
                return Limit(articleParagraphs);
            }

            var mainParagraphs = BestParagraphs(document, new[] { "main" });
            if (IsBodyEnough(mainParagraphs))
            {
                return Limit(mainParagraphs);
            }

            return Limit(CollectParagraphs(document.Body ?? document.DocumentElement));
        }

        private static List<string> BestParagraphs(IDocument document, IEnumerable<string> selectors)
        {
            var best = new List<string>();
            var bestLength = 0;

            foreach (var selector in selectors)
            {
                foreach (var element in document.QuerySelectorAll(selector))
                {
                    var paragraphs = CollectParagraphs(element);
                    var length = paragraphs.Sum(paragraph => paragraph.Length);
                    if (length > bestLength)
                    {
                        best = paragraphs;
                        bestLength = length;
                    }
                }
            }

            return best;
        }

        private static List<string> CollectParagraphs(IElement? scope)
        {
            var paragraphs = new List<string>();
            if (scope is null)
            {
                return paragraphs;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in scope.QuerySelectorAll("p"))
            {
                if (HasNoiseToken(node))
                {
                    continue;
                }

                var text = Clean(node.TextContent);
                if (text is null ||
                    text.Length < MinParagraphLength ||
                    BoilerplateRegex.IsMatch(text) ||
                    IsMostlyLink(node))
                {
                    continue;
                }

                if (seen.Add(text))
                {
                    paragraphs.Add(text);
                }
            }

            return paragraphs;
        }

        /// <summary>
        /// Removes navigation, sharing widgets, comment sections and other page chrome
        /// so they never end up inside the article body. Known body containers (and
        /// their ancestors) are protected: some sites wrap the note in elements with
        /// misleading names such as "sidebar__main".
        /// </summary>
        private static void RemoveNoise(IDocument document)
        {
            var protectedNodes = new HashSet<INode>();

            foreach (var selector in BodySelectors)
            {
                foreach (var element in document.QuerySelectorAll(selector))
                {
                    INode? current = element;
                    while (current is not null)
                    {
                        protectedNodes.Add(current);
                        current = current.Parent;
                    }
                }
            }

            var junk = document.QuerySelectorAll(
                "script, style, noscript, template, svg, iframe, nav, footer, aside, form, button");

            foreach (var element in junk.ToList())
            {
                element.Remove();
            }

            foreach (var element in document.QuerySelectorAll("[class], [id]").ToList())
            {
                if (element.ParentElement is not null &&
                    !protectedNodes.Contains(element) &&
                    HasNoiseToken(element))
                {
                    element.Remove();
                }
            }
        }

        private static List<string> ExtractJsonLdParagraphs(IDocument document)
        {
            foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
            {
                JsonDocument json;
                try
                {
                    json = JsonDocument.Parse(script.TextContent);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (json)
                {
                    foreach (var node in EnumerateJsonLdNodes(json.RootElement))
                    {
                        if (node.ValueKind != JsonValueKind.Object ||
                            !node.TryGetProperty("articleBody", out var body) ||
                            body.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var paragraphs = SplitParagraphs(body.GetString());
                        if (paragraphs.Count > 0)
                        {
                            return paragraphs;
                        }
                    }
                }
            }

            return new List<string>();
        }

        private static string? GetString(JsonElement node, string property) =>
            node.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static IEnumerable<JsonElement> EnumerateJsonLdNodes(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    yield return item;
                }

                yield break;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            yield return root;

            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                {
                    yield return item;
                }
            }
        }

        private static List<string> SplitParagraphs(string? body)
        {
            var paragraphs = new List<string>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return paragraphs;
            }

            foreach (var chunk in Regex.Split(body, @"\r?\n\s*\r?\n|\r?\n"))
            {
                var text = Clean(chunk);
                if (text is { Length: >= MinParagraphLength } && !BoilerplateRegex.IsMatch(text))
                {
                    paragraphs.Add(text);
                }
            }

            return paragraphs;
        }

        private static bool IsBodyEnough(List<string> paragraphs) =>
            paragraphs.Count > 0 &&
            paragraphs.Sum(paragraph => paragraph.Length) >= MinBodyCharacters;

        private static bool IsMostlyLink(IElement paragraph)
        {
            var link = paragraph.QuerySelector("a[href]");
            if (link is null)
            {
                return false;
            }

            var linkText = Clean(link.TextContent);
            return linkText is not null &&
                   linkText.Length >= paragraph.TextContent.Length * 0.7;
        }

        private static bool HasNoiseToken(IElement element) =>
            HasNoiseToken(element.ClassName) || HasNoiseToken(element.Id);

        private static bool HasNoiseToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var token in value.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (NoiseTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> Limit(List<string> paragraphs)
        {
            if (paragraphs.Count == 0)
            {
                return Array.Empty<string>();
            }

            var limited = new List<string>();
            var total = 0;

            foreach (var paragraph in paragraphs)
            {
                if (limited.Count >= MaxParagraphs || total >= MaxCharacters)
                {
                    break;
                }

                limited.Add(paragraph);
                total += paragraph.Length;
            }

            return limited;
        }

        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var cleaned = WhitespaceRegex.Replace(value, " ").Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }
    }
}
