using System.Globalization;
using System.Text;

namespace NewsAggregator.Services.Grouping
{
    /// <summary>
    /// Conservative headline similarity used to decide whether two outlets are
    /// telling the same story.
    ///
    /// The measure is a token-set ratio: the significant words of both headlines
    /// are compared by character overlap against their shared words. This keeps
    /// editorial rewording of the same event together ("León XIV cerró su gira"
    /// vs "León XIV se despidió de Perú") while leaving different events apart,
    /// even when they mention the same person.
    ///
    /// The character overlap uses the Ratcliff/Obershelp ratio, the same
    /// measure Python's <c>difflib.SequenceMatcher</c> exposes, so the threshold
    /// was tuned against the real feeds with the exact same number.
    /// </summary>
    public static class HeadlineSimilarity
    {
        /// <summary>Below this, the ratio is not worth comparing characters for.</summary>
        public const int MinimumSharedWords = 3;

        /// <summary>
        /// Words that carry no topic. Kept short on purpose: every removed word
        /// raises the similarity of headlines that only share filler.
        /// </summary>
        private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
        {
            "el", "la", "los", "las", "un", "una", "unos", "unas",
            "de", "del", "al", "y", "o", "u", "en", "con", "por", "para",
            "que", "se", "su", "sus", "lo", "como", "mas", "pero", "sin",
            "sobre", "entre", "cuando", "donde", "este", "esta", "estos",
            "estas", "ese", "esa", "esos", "esas", "es", "son", "fue",
            "tras", "desde", "hasta", "ya", "hay", "muy", "no", "ni", "le",
            "les", "me", "mi", "te", "tu", "yo", "cual", "cuales", "quien",
            "quienes", "porque", "aunque", "ante", "bajo", "cabe", "sino"
        };

        /// <summary>
        /// Splits a headline into its significant words: lowercase, without
        /// accents or punctuation, four letters or more, stop words removed.
        /// </summary>
        public static IReadOnlySet<string> SignificantWords(string? headline)
        {
            var words = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(headline))
            {
                return words;
            }

            var normalized = RemoveDiacritics(headline.ToLowerInvariant());
            var builder = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == 'ñ' ? c : ' ');
            }

            foreach (var word in builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length >= 3 && !StopWords.Contains(word))
                {
                    words.Add(word);
                }
            }

            return words;
        }

        /// <summary>
        /// Returns 0 when the headlines share fewer than
        /// <see cref="MinimumSharedWords"/> significant words.
        /// </summary>
        public static double TokenSetRatio(string? left, string? right)
            => TokenSetRatio(SignificantWords(left), SignificantWords(right));

        public static double TokenSetRatio(IReadOnlySet<string> left, IReadOnlySet<string> right)
        {
            if (left.Count == 0 || right.Count == 0)
            {
                return 0;
            }

            var shared = left.Where(right.Contains).OrderBy(w => w, StringComparer.Ordinal).ToList();
            if (shared.Count < MinimumSharedWords)
            {
                return 0;
            }

            var sharedText = string.Join(' ', shared);
            var leftText = string.Join(' ', left.OrderBy(w => w, StringComparer.Ordinal));
            var rightText = string.Join(' ', right.OrderBy(w => w, StringComparer.Ordinal));

            // Half the weight on how completely each side is covered by the
            // shared words. A short headline fully contained in a long one
            // therefore still scores high.
            return (SequenceRatio(sharedText, leftText) + SequenceRatio(sharedText, rightText)) / 2.0;
        }

        /// <summary>
        /// Ratcliff/Obershelp similarity: twice the number of matched characters
        /// over the total length, as in Python's difflib.
        /// </summary>
        public static double SequenceRatio(string left, string right)
        {
            if (left.Length == 0 && right.Length == 0)
            {
                return 1;
            }

            var total = left.Length + right.Length;
            if (total == 0)
            {
                return 1;
            }

            var matched = CountMatches(left.AsSpan(), right.AsSpan());
            return 2.0 * matched / total;
        }

        private static int CountMatches(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
        {
            var (leftStart, rightStart, size) = FindLongestMatch(left, right);
            if (size == 0)
            {
                return 0;
            }

            var before = CountMatches(
                left[..leftStart],
                right[..rightStart]);
            var after = CountMatches(
                left[(leftStart + size)..],
                right[(rightStart + size)..]);

            return before + size + after;
        }

        /// <summary>
        /// Longest common substring, returning the first (top-left) occurrence on
        /// ties so the recursion stays deterministic.
        /// </summary>
        private static (int LeftStart, int RightStart, int Size) FindLongestMatch(
            ReadOnlySpan<char> left,
            ReadOnlySpan<char> right)
        {
            var bestSize = 0;
            var bestLeft = 0;
            var bestRight = 0;

            // lengths[i][j] = length of the common suffix ending at left[i-1], right[j-1]
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];

            for (var i = 1; i <= left.Length; i++)
            {
                for (var j = 1; j <= right.Length; j++)
                {
                    if (left[i - 1] == right[j - 1])
                    {
                        current[j] = previous[j - 1] + 1;
                        if (current[j] > bestSize)
                        {
                            bestSize = current[j];
                            bestLeft = i - current[j];
                            bestRight = j - current[j];
                        }
                    }
                    else
                    {
                        current[j] = 0;
                    }
                }

                (previous, current) = (current, previous);
                Array.Clear(current);
            }

            return (bestLeft, bestRight, bestSize);
        }

        private static string RemoveDiacritics(string value)
        {
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
