namespace FindThatBook.Domain.Text;

/// <summary>
/// Deterministic string-similarity metrics used by the ranking engine. Everything
/// here operates on already-normalized text and returns a value in [0, 1].
/// </summary>
public static class TextSimilarity
{
    /// <summary>
    /// Similarity between two free-text strings. Combines a token-set (Jaccard)
    /// view — robust to word order and subtitles — with edit-distance similarity
    /// on the whole string, which rewards near-identical spellings.
    /// </summary>
    public static double Similarity(string? left, string? right)
    {
        var a = TextNormalizer.Normalize(left);
        var b = TextNormalizer.Normalize(right);

        if (a.Length == 0 || b.Length == 0)
        {
            return 0d;
        }

        if (a == b)
        {
            return 1d;
        }

        var jaccard = TokenJaccard(a, b);
        var edit = EditSimilarity(a, b);
        return Math.Max(jaccard, edit * 0.9);
    }

    /// <summary>Jaccard overlap of the token sets of two strings.</summary>
    public static double TokenJaccard(string? left, string? right)
    {
        var a = TextNormalizer.Tokenize(left).ToHashSet();
        var b = TextNormalizer.Tokenize(right).ToHashSet();

        if (a.Count == 0 || b.Count == 0)
        {
            return 0d;
        }

        var intersection = a.Intersect(b).Count();
        var union = a.Count + b.Count - intersection;
        return (double)intersection / union;
    }

    /// <summary>
    /// True when <paramref name="candidate"/> contains all tokens of
    /// <paramref name="query"/> (subtitle-tolerant "The Hobbit" ⊆ "The Hobbit: There and Back Again").
    /// </summary>
    public static bool ContainsAllTokens(string? query, string? candidate)
    {
        var queryTokens = TextNormalizer.SignificantTokens(query);
        if (queryTokens.Count == 0)
        {
            return false;
        }

        var candidateTokens = TextNormalizer.Tokenize(candidate).ToHashSet();
        return queryTokens.All(candidateTokens.Contains);
    }

    /// <summary>Edit-distance similarity, normalized to [0, 1].</summary>
    public static double EditSimilarity(string? left, string? right)
    {
        var a = left ?? string.Empty;
        var b = right ?? string.Empty;

        if (a.Length == 0 && b.Length == 0)
        {
            return 1d;
        }

        var distance = Levenshtein(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 1d : 1d - (double)distance / maxLen;
    }

    /// <summary>Classic Levenshtein edit distance with an O(min(n,m)) memory footprint.</summary>
    public static int Levenshtein(string a, string b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        // Ensure b is the shorter string to minimize the row buffer.
        if (b.Length > a.Length)
        {
            (a, b) = (b, a);
        }

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
