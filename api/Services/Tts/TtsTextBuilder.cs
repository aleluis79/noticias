using System.Security.Cryptography;
using System.Text;
using NewsAggregator.Models;

namespace NewsAggregator.Services.Tts
{
    /// <summary>Builds the spoken text for one segment of an article.</summary>
    public static class TtsTextBuilder
    {
        private static readonly HashSet<string> GenericAuthors = new(StringComparer.OrdinalIgnoreCase)
        {
            "redaccion", "staff", "newsroom", "agencia", "agencias", "editorial"
        };

        public static string Build(
            TtsSegment segment,
            ArticleContent content,
            string? sourceName,
            string? titleOverride = null,
            string? authorOverride = null)
        {
            return segment.Part switch
            {
                TtsSegmentPart.Title => Normalize(
                    string.IsNullOrWhiteSpace(titleOverride) ? content.Title : titleOverride),
                TtsSegmentPart.Attribution => BuildAttribution(
                    string.IsNullOrWhiteSpace(authorOverride) ? content.Author : authorOverride,
                    sourceName),
                TtsSegmentPart.Body => BuildBody(content, segment.Index),
                _ => string.Empty
            };
        }

        public static string BuildAttribution(string? author, string? sourceName)
        {
            var visibleAuthor = VisibleAuthor(author, sourceName);
            var source = Normalize(sourceName);

            if (visibleAuthor is not null && source.Length > 0)
            {
                return $"Por {visibleAuthor}, {source}.";
            }

            if (visibleAuthor is not null)
            {
                return $"Por {visibleAuthor}.";
            }

            return source.Length > 0 ? $"{source}." : string.Empty;
        }

        private static string BuildBody(ArticleContent content, int? index)
        {
            if (index is null || index < 0 || index >= content.Paragraphs.Count)
            {
                return string.Empty;
            }

            return Normalize(content.Paragraphs[index.Value]);
        }

        private static string? VisibleAuthor(string? author, string? sourceName)
        {
            var normalized = Normalize(author);
            if (normalized.Length == 0)
            {
                return null;
            }

            var comparable = RemoveDiacritics(normalized).ToLowerInvariant();
            if (GenericAuthors.Contains(comparable))
            {
                return null;
            }

            var source = RemoveDiacritics(Normalize(sourceName)).ToLowerInvariant();
            if (source.Length > 0 &&
                (comparable == source || comparable.StartsWith($"{source} ", StringComparison.Ordinal)))
            {
                return null;
            }

            return normalized;
        }

        public static string Normalize(string? value) =>
            string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string RemoveDiacritics(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);

            foreach (var character in decomposed)
            {
                if (char.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    /// <summary>Stable cache key for an article segment and voice model.</summary>
    public static class TtsCacheKey
    {
        public static string For(string scope, string modelPath)
        {
            var bytes = Encoding.UTF8.GetBytes($"{scope}\n{modelPath}");
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
