using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

        private const int MinParagraphLength = 40;
        private const int MinBodyCharacters = 300;
        private const int MaxParagraphs = 200;
        private const int MaxCharacters = 30000;

        /// <summary>Containers that news sites usually wrap the body of a note in.</summary>
        private static readonly string[] BodySelectors =
        {
            "[itemprop='articleBody']",
            "[data-testid='article-body']",
            "[data-testid='articleBody']",
            ".article-body", ".article__body", ".article-body-content",
            ".article-content", ".article__content", ".article-text",
            ".articletext", ".articleBody", ".nota-cuerpo", ".nota__cuerpo",
            ".nota-body", ".body-nota", ".cuerpo-nota", ".story-body",
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
        private readonly ILogger<ArticleContentService> _logger;

        private readonly ConcurrentDictionary<string, CacheEntry> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public ArticleContentService(
            NewsAggregationService pageFetcher,
            ILogger<ArticleContentService> logger)
        {
            _pageFetcher = pageFetcher;
            _logger = logger;
        }

        private sealed record CacheEntry(DateTime ExpiresAt, ArticleContent? Content);

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

            var paragraphs = html is null
                ? Array.Empty<string>()
                : ExtractParagraphs(html);

            var content = paragraphs.Count == 0
                ? null
                : new ArticleContent
                {
                    Url = cacheKey,
                    Author = html is null ? string.Empty : ExtractAuthor(html) ?? string.Empty,
                    Paragraphs = paragraphs
                };

            _cache[cacheKey] = new CacheEntry(
                DateTime.UtcNow.Add(content is null ? FailureCacheDuration : CacheDuration),
                content);

            if (content is null)
            {
                _logger.LogInformation("No article body could be extracted from {Url}", cacheKey);
            }

            return content;
        }

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
