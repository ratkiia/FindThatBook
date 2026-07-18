using System.Text.Json;
using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using Microsoft.Extensions.Logging;

namespace FindThatBook.Infrastructure.Gemini;

/// <summary>
/// Uses Gemini to turn a messy query into structured {title?, author?, keywords[]}.
/// </summary>
public sealed class GeminiFieldExtractor : IAiFieldExtractor
{
    private readonly GeminiApi _api;
    private readonly ILogger<GeminiFieldExtractor> _logger;

    public GeminiFieldExtractor(GeminiApi api, ILogger<GeminiFieldExtractor> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<ExtractedFields> ExtractAsync(string rawQuery, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(rawQuery);
        var text = await _api.GenerateJsonAsync(prompt, cancellationToken);

        try
        {
            using var doc = GeminiJson.Parse(text);
            var root = doc.RootElement;

            var title = ReadString(root, "title");
            var author = ReadString(root, "author");
            var keywords = ReadStringArray(root, "keywords");

            return new ExtractedFields(title, author, keywords);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini extraction response: {Response}", text);
            throw new InvalidOperationException("Gemini returned malformed JSON for field extraction.", ex);
        }
    }

    private static string BuildPrompt(string rawQuery) =>
        $$"""
        You are helping a library search engine interpret a messy, free-text book query.
        Extract the most likely intended fields. The query may contain only an author,
        only a title, both, or character/keyword hints.

        Rules:
        - "title": the intended book title if identifiable, else null.
        - "author": the intended author's real name. Expand partials and resolve character
          or nickname hints to the real author when confident (e.g. "mark huckleberry" -> "Mark Twain").
          If unsure, use null.
        - "keywords": any remaining meaningful tokens (adjectives, formats like "illustrated"
          or "deluxe", years, character names). Exclude tokens already used as title/author.
        - Respond with ONLY a JSON object of the form:
          {"title": string|null, "author": string|null, "keywords": string[]}

        Query: "{{rawQuery}}"
        """;

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element))
        {
            return null;
        }

        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var value = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                list.Add(value.Trim());
            }
        }

        return list;
    }
}
