using System.Text.Json;

namespace FindThatBook.Infrastructure.Gemini;

/// <summary>Helpers for coping with the ways an LLM can wrap or malform JSON.</summary>
internal static class GeminiJson
{
    /// <summary>
    /// Extracts a JSON document from a model response, Returns a parsed <see cref="JsonDocument"/>.
    /// </summary>
    public static JsonDocument Parse(string modelText)
    {
        var cleaned = Strip(modelText);
        return JsonDocument.Parse(cleaned);
    }

    private static string Strip(string text)
    {
        var trimmed = text.Trim();

        // Remove ```json ... ``` or ``` ... ``` fences.
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }

            trimmed = trimmed.Trim();
        }

        // If there is still surrounding prose, slice from the first bracket to the last.
        var start = trimmed.IndexOfAny(new[] { '{', '[' });
        var end = trimmed.LastIndexOfAny(new[] { '}', ']' });
        if (start >= 0 && end > start)
        {
            trimmed = trimmed[start..(end + 1)];
        }

        return trimmed;
    }
}
