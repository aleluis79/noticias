using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using NewsAggregator.Models;

namespace NewsAggregator.Services.Parsers
{
    /// <summary>
    /// Base parser for a news home page. It extracts the most prominent headlines using,
    /// in order of reliability: JSON-LD structured data, article cards, headings with links
    /// and finally article-like anchors.
    /// </summary>
    public abstract class HomePageParser : IParser
    {
        private const int DefaultMaxArticles = 20;

        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex NonAlphanumericRegex = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled);
        private static readonly Regex NavigationTitleRegex = new(
            @"^(inicio|home|ultimas noticias|últimas noticias|suscribite|suscribirse|suscribase|iniciar sesion|iniciar sesión|ingresar|registrarse|crear cuenta|menu|menú|buscar|newsletter|contacto|quienes somos|quiénes somos|terminos|términos|privacidad|politica de privacidad)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly string[] IgnoredPathTerms =
        {
            "/tag/", "/tags/", "/autor/", "/autores/", "/author/", "/perfil/", "/perfiles/",
            "/calculadora", "/newsletter", "/suscrib", "/suscripcion", "/rss", "/mapa-del-sitio",
            "/terminos", "/terminos-y-condiciones", "/privacidad", "/mercadosonline", "/buscar",
            "/search", "/login", "/ingresar", "/registro", "/quienes-somos"
        };

        private static readonly string[] NonArticleExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".pdf", ".mp3", ".mp4", ".zip"
        };

        /// <summary>Class names that news sites commonly use for the article summary.</summary>
        private static readonly string[] SummaryClassTerms =
        {
            "bajada", "copete", "summary", "description", "subtitle", "subhead", "lead", "deck", "resumen"
        };

        /// <summary>Class names that indicate the element is not a summary.</summary>
        private static readonly string[] SummaryExcludedClassTerms =
        {
            "author", "byline", "date", "time", "title", "volanta", "kicker", "badge", "tag"
        };

        private readonly record struct RawCandidate(string? Title, string? Url, string? Summary, DateTime? Published);

        public abstract string SourceName { get; }

        public abstract string BaseUrl { get; }

        /// <summary>Maximum number of headlines returned per source.</summary>
        protected virtual int MaxArticles => DefaultMaxArticles;

        /// <summary>Extra path fragments that do not point to a news article on this site.</summary>
        protected virtual string[] AdditionalIgnoredPathTerms => Array.Empty<string>();

        public IReadOnlyList<NewsArticle> Parse(string rawHtml)
        {
            if (string.IsNullOrWhiteSpace(rawHtml))
            {
                return Array.Empty<NewsArticle>();
            }

            var document = new HtmlParser().ParseDocument(rawHtml);
            var candidates = new List<RawCandidate>();

            CollectJsonLd(document, candidates);
            CollectHeadings(document, candidates);
            CollectArticleCards(document, candidates);

            if (candidates.Count < MaxArticles)
            {
                CollectArticleAnchors(document, candidates);
            }

            return Materialize(candidates);
        }

        /// <summary>
        /// Last chance fallback: any anchor whose URL looks like an article and whose text
        /// is long enough to be a headline.
        /// </summary>
        private void CollectArticleAnchors(IDocument document, List<RawCandidate> candidates)
        {
            foreach (var anchor in document.QuerySelectorAll("a[href]"))
            {
                var title = Clean(anchor.GetAttribute("aria-label"))
                            ?? Clean(anchor.GetAttribute("title"))
                            ?? Clean(anchor.TextContent);

                candidates.Add(new RawCandidate(title, anchor.GetAttribute("href"), null, null));
            }
        }

        private void CollectHeadings(IDocument document, List<RawCandidate> candidates)
        {
            foreach (var heading in document.QuerySelectorAll("h1, h2, h3, h4"))
            {
                // Some sites use a heading for the kicker/volanta of a card; it is not the headline.
                if (IsKicker(heading))
                {
                    continue;
                }

                var anchor = FindAnchor(heading);
                var title = Clean(anchor?.GetAttribute("aria-label")) ?? ExtractHeadingText(heading);
                var url = anchor?.GetAttribute("href");
                var summary = FindNearbySummary(heading);

                candidates.Add(new RawCandidate(title, url, summary, null));
            }
        }

        private void CollectArticleCards(IDocument document, List<RawCandidate> candidates)
        {
            foreach (var card in document.QuerySelectorAll("article"))
            {
                var heading = card.QuerySelectorAll("h1, h2, h3, h4")
                    .FirstOrDefault(candidate => !IsKicker(candidate));
                var anchor = card.QuerySelector("a[href]");

                // Prefer the most complete version of the headline: headings sometimes
                // contain only a short label while the link carries the full title.
                var title = PickBestTitle(
                    heading is null ? null : ExtractHeadingText(heading),
                    anchor?.GetAttribute("aria-label"),
                    anchor?.GetAttribute("title"));

                var url = heading is null
                    ? anchor?.GetAttribute("href")
                    : FindAnchor(heading)?.GetAttribute("href") ?? anchor?.GetAttribute("href");

                candidates.Add(new RawCandidate(title, url, FindSummary(card), null));
            }
        }

        private void CollectJsonLd(IDocument document, List<RawCandidate> candidates)
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
                        if (node.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var types = GetTypes(node);
                        if (types.Count == 0)
                        {
                            continue;
                        }

                        if (types.Contains("ItemList"))
                        {
                            CollectItemList(node, candidates);
                        }
                        else if (types.Any(IsArticleType))
                        {
                            candidates.Add(new RawCandidate(
                                GetString(node, "headline") ?? GetString(node, "name"),
                                GetString(node, "url") ?? GetString(node, "@id"),
                                GetString(node, "description"),
                                GetDate(node, "datePublished")));
                        }
                    }
                }
            }
        }

        private static void CollectItemList(JsonElement node, List<RawCandidate> candidates)
        {
            if (!node.TryGetProperty("itemListElement", out var elements) || elements.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                // ListItem can either hold the data directly or nest it under "item".
                var target = element;
                if (element.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object)
                {
                    target = item;
                }

                var url = GetString(target, "url") ?? GetString(target, "@id") ?? GetString(element, "url");
                candidates.Add(new RawCandidate(
                    GetString(target, "name") ?? GetString(target, "headline") ?? GetString(element, "name"),
                    url,
                    GetString(target, "description") ?? GetString(element, "description"),
                    GetDate(target, "datePublished")));
            }
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

        private static List<string> GetTypes(JsonElement node)
        {
            var types = new List<string>();
            if (!node.TryGetProperty("@type", out var type))
            {
                return types;
            }

            if (type.ValueKind == JsonValueKind.String)
            {
                types.Add(type.GetString() ?? string.Empty);
            }
            else if (type.ValueKind == JsonValueKind.Array)
            {
                foreach (var value in type.EnumerateArray())
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        types.Add(value.GetString() ?? string.Empty);
                    }
                }
            }

            return types;
        }

        private static bool IsArticleType(string type) =>
            type.EndsWith("Article", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("BlogPosting", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("ReportageNewsArticle", StringComparison.OrdinalIgnoreCase);

        private static string? GetString(JsonElement node, string property) =>
            node.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static DateTime? GetDate(JsonElement node, string property)
        {
            var raw = GetString(node, property);
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
                ? date
                : null;
        }

        private IReadOnlyList<NewsArticle> Materialize(List<RawCandidate> candidates)
        {
            var articles = new List<NewsArticle>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in candidates)
            {
                var title = CleanCandidateTitle(candidate.Title);
                if (!IsUsableTitle(title))
                {
                    continue;
                }

                var url = ResolveArticleUrl(candidate.Url);
                if (url is null)
                {
                    continue;
                }

                var urlKey = url.GetLeftPart(UriPartial.Path);
                if (!seenUrls.Add(urlKey) || !seenTitles.Add(NormalizeKey(title!)))
                {
                    continue;
                }

                var summary = Clean(candidate.Summary) ?? string.Empty;
                if (summary.Length < 30 ||
                    NormalizeKey(summary) == NormalizeKey(title!) ||
                    NormalizeKey(summary).StartsWith(NormalizeKey(title!), StringComparison.Ordinal))
                {
                    summary = string.Empty;
                }

                articles.Add(new NewsArticle
                {
                    Title = title!,
                    Summary = summary,
                    OriginalUrl = url.ToString(),
                    SourceName = SourceName,
                    // JSON-LD gives the real publication date when available;
                    // home pages rarely expose it, and an unknown date stays
                    // unknown instead of becoming the retrieval time.
                    PublishedDate = candidate.Published
                });

                if (articles.Count >= MaxArticles)
                {
                    break;
                }
            }

            return articles;
        }

        private string? CleanCandidateTitle(string? title)
        {
            var cleaned = Clean(title);
            if (cleaned is null)
            {
                return null;
            }

            return cleaned.Equals(SourceName, StringComparison.OrdinalIgnoreCase) ? null : cleaned;
        }

        private bool IsUsableTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            if (title.Length < 18 || title.Length > 240)
            {
                return false;
            }

            return !NavigationTitleRegex.IsMatch(title);
        }

        private Uri? ResolveArticleUrl(string? href)
        {
            if (string.IsNullOrWhiteSpace(href))
            {
                return null;
            }

            if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var candidate))
            {
                return null;
            }

            var resolved = candidate.IsAbsoluteUri ? candidate : new Uri(new Uri(BaseUrl), candidate);
            if (resolved.Scheme is not ("http" or "https"))
            {
                return null;
            }

            if (!HostsMatch(resolved.Host, new Uri(BaseUrl).Host))
            {
                return null;
            }

            var path = resolved.AbsolutePath;
            if (path.Length <= 1 ||
                IgnoredPathTerms.Any(term => path.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                AdditionalIgnoredPathTerms.Any(term => path.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            if (NonArticleExtensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            // "friendly" article slugs are usually long. Strip the _0_<id> suffix used by
            // Clarín and La Voz before measuring.
            var slug = Path.GetFileNameWithoutExtension(segments[^1]);
            var underscore = slug.IndexOf('_');
            if (underscore > 0)
            {
                slug = slug[..underscore];
            }

            return slug.Length >= 12 ? resolved : null;
        }

        private static bool HostsMatch(string host, string baseHost) =>
            string.Equals(TrimWww(host), TrimWww(baseHost), StringComparison.OrdinalIgnoreCase);

        private static string TrimWww(string host) =>
            host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;

        private static IElement? FindAnchor(IElement element)
        {
            var own = element.QuerySelector("a[href]");
            if (own is not null)
            {
                return own;
            }

            // A link that wraps the headline is always about that headline. Looking at
            // siblings (e.g. topic chips next to the news grid) produced wrong links,
            // so only real ancestors are considered here.
            var current = element.ParentElement;
            while (current is not null &&
                   !current.TagName.Equals("BODY", StringComparison.OrdinalIgnoreCase) &&
                   !current.TagName.Equals("HTML", StringComparison.OrdinalIgnoreCase))
            {
                if (current.TagName.Equals("A", StringComparison.OrdinalIgnoreCase) &&
                    current.GetAttribute("href") is not null)
                {
                    return current;
                }

                current = current.ParentElement;
            }

            return null;
        }

        private static string? FindNearbySummary(IElement heading)
        {
            var scope = heading.ParentElement;
            var summary = FindSummary(scope);
            if (summary is not null)
            {
                return summary;
            }

            // Headlines are often wrapped in a <header> while the summary lives in the
            // card body right outside of it.
            var parent = scope?.ParentElement;
            if (parent is null ||
                parent.TagName.Equals("BODY", StringComparison.OrdinalIgnoreCase) ||
                parent.TagName.Equals("MAIN", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return FindSummary(parent);
        }

        /// <summary>
        /// Looks for the article summary ("bajada") inside the given container. Paragraphs
        /// are preferred; class names commonly used by news sites are the fallback.
        /// </summary>
        private static string? FindSummary(IElement? scope)
        {
            if (scope is null)
            {
                return null;
            }

            foreach (var paragraph in scope.QuerySelectorAll("p"))
            {
                if (HasExcludedClass(paragraph))
                {
                    continue;
                }

                var text = Clean(paragraph.TextContent);
                if (text is { Length: >= 40 and <= 500 })
                {
                    return text;
                }
            }

            foreach (var element in scope.QuerySelectorAll("[class]"))
            {
                var cssClass = element.GetAttribute("class") ?? string.Empty;

                // Skip kickers, bylines and wrappers that contain the headline itself.
                if (IsKicker(element) ||
                    HasExcludedClass(element) ||
                    element.QuerySelector("h1, h2, h3, h4") is not null ||
                    !SummaryClassTerms.Any(term => ClassMentions(cssClass, term)))
                {
                    continue;
                }

                var text = Clean(element.TextContent);
                if (text is { Length: >= 40 and <= 500 })
                {
                    return text;
                }
            }

            return null;
        }

        private static bool HasExcludedClass(IElement element)
        {
            var cssClass = element.GetAttribute("class");
            return cssClass is not null && SummaryExcludedClassTerms.Any(term => ClassMentions(cssClass, term));
        }

        private static string? PickBestTitle(params string?[] titles)
        {
            return titles
                .Select(StripSitePrefix)
                .Where(title => !string.IsNullOrWhiteSpace(title))
                .OrderByDescending(title => title!.Length)
                .FirstOrDefault();
        }

        /// <summary>
        /// Checks whether a CSS class mentions a term as a real token. A plain Contains
        /// check is not enough: "lead" would match utility classes such as "leading-7".
        /// </summary>
        private static bool ClassMentions(string cssClass, string term)
        {
            foreach (var token in cssClass.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Equals(term, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                foreach (var part in token.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.Equals(term, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Removes prefixes such as "ámbito.com | " that some sites put in titles.</summary>
        private static string? StripSitePrefix(string? title)
        {
            var cleaned = Clean(title);
            if (cleaned is null)
            {
                return null;
            }

            var separator = cleaned.IndexOf(" | ", StringComparison.Ordinal);
            return separator is > 0 and <= 30 && cleaned.Length - separator > 20
                ? cleaned[(separator + 3)..].Trim()
                : cleaned;
        }

        /// <summary>
        /// Reads the heading text, ignoring kicker/overline spans used by several sites
        /// (e.g. "Alerta Cedear. Wall Street le baja ...").
        /// </summary>
        private static string ExtractHeadingText(IElement heading)
        {
            var builder = new StringBuilder();
            AppendText(heading, builder);
            return Clean(builder.ToString()) ?? string.Empty;
        }

        private static void AppendText(INode node, StringBuilder builder)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child is IElement element && IsKicker(element))
                {
                    continue;
                }

                if (child.NodeType == NodeType.Text)
                {
                    builder.Append(child.TextContent).Append(' ');
                }
                else if (child.NodeType == NodeType.Element)
                {
                    AppendText(child, builder);
                }
            }
        }

        private static bool IsKicker(IElement element)
        {
            var cssClass = element.GetAttribute("class");
            return cssClass is not null &&
                   (cssClass.Contains("kicker", StringComparison.OrdinalIgnoreCase) ||
                    cssClass.Contains("volanta", StringComparison.OrdinalIgnoreCase) ||
                    cssClass.Contains("overline", StringComparison.OrdinalIgnoreCase) ||
                    cssClass.Contains("eyebrow", StringComparison.OrdinalIgnoreCase));
        }

        private static string? Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Some feeds ship HTML entities in their JSON-LD ("Newell&#x27;s"),
            // sometimes double-encoded ("&amp;#x27;"). Decode until stable so
            // they never reach the reader.
            var decoded = value;
            for (var pass = 0; pass < 2; pass++)
            {
                var next = System.Net.WebUtility.HtmlDecode(decoded);
                if (next == decoded)
                {
                    break;
                }

                decoded = next;
            }

            var cleaned = WhitespaceRegex.Replace(decoded, " ").Trim();
            return cleaned.Length == 0 ? null : cleaned;
        }

        private static string NormalizeKey(string value) =>
            NonAlphanumericRegex.Replace(value.ToLowerInvariant(), string.Empty);
    }
}
