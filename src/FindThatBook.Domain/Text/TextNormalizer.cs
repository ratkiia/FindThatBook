using System.Globalization;
using System.Text;

namespace FindThatBook.Domain.Text;

/// <summary>
/// Pure text-normalization rules shared by extraction, matching and ranking.
/// Deterministic and side-effect free so it can be unit tested in isolation.
/// </summary>
public static class TextNormalizer
{
    // Tokens that add noise to book queries but rarely help matching.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "and", "or", "to", "in", "on", "for", "with", "by"
    };

    /// <summary>
    /// Lowercases, strips diacritics, removes punctuation and collapses whitespace.
    /// "Tolkien Hóbbit, Illustrated!" -> "tolkien hobbit illustrated".
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var stripped = RemoveDiacritics(input);
        var builder = new StringBuilder(stripped.Length);
        var previousWasSpace = false;

        foreach (var ch in stripped)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                // Any run of punctuation/whitespace collapses to a single space.
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Splits normalized text into tokens.</summary>
    public static IReadOnlyList<string> Tokenize(string? input) =>
        Normalize(input).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Tokens with stop-words removed. Used for keyword/overlap comparisons.</summary>
    public static IReadOnlyList<string> SignificantTokens(string? input) =>
        Tokenize(input).Where(t => !StopWords.Contains(t)).ToList();

    public static bool IsStopWord(string token) => StopWords.Contains(token);

    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
