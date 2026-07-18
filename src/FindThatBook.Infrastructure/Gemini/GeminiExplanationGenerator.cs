using System.Text;
using System.Text.Json;
using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using Microsoft.Extensions.Logging;

namespace FindThatBook.Infrastructure.Gemini;

/// <summary>
/// Uses Gemini to write a short, grounded "why this book" explanation for each candidate. The prompt supplies the deterministic ranking signals and forbids invention, so explanations stay faithful to the fetched fields.
/// </summary>
public sealed class GeminiExplanationGenerator : IAiExplanationGenerator
{
    private readonly GeminiApi _api;
    private readonly ILogger<GeminiExplanationGenerator> _logger;

    public GeminiExplanationGenerator(GeminiApi api, ILogger<GeminiExplanationGenerator> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> ExplainAsync(
        ExtractedFields query,
        IReadOnlyList<ScoredWork> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var prompt = BuildPrompt(query, candidates);
        var text = await _api.GenerateJsonAsync(prompt, cancellationToken);

        // Map the ids we sent (work ids) back to canonical work keys.
        var byWorkId = candidates.ToDictionary(c => c.Work.WorkId, c => c.Work.WorkKey);
        var result = new Dictionary<string, string>();

        try
        {
            using var doc = GeminiJson.Parse(text);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var explanation = item.TryGetProperty("explanation", out var exEl) ? exEl.GetString() : null;

                if (!string.IsNullOrWhiteSpace(id) &&
                    !string.IsNullOrWhiteSpace(explanation) &&
                    byWorkId.TryGetValue(id!, out var workKey))
                {
                    result[workKey] = explanation!.Trim();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini explanation response: {Response}", text);
            throw new InvalidOperationException("Gemini returned malformed JSON for explanations.", ex);
        }

        return result;
    }

    private static string BuildPrompt(ExtractedFields query, IReadOnlyList<ScoredWork> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You explain why each candidate book matches a reader's messy query.");
        sb.AppendLine("Write ONE grounded sentence per candidate (max two). Use ONLY the facts provided");
        sb.AppendLine("below — do not invent plot, awards, or details. When a person is only a contributor");
        sb.AppendLine("(illustrator/editor/adaptor) rather than the primary author, say so.");
        sb.AppendLine();
        sb.AppendLine($"Reader query interpreted as: title={Quote(query.Title)}, author={Quote(query.Author)}, keywords=[{string.Join(", ", query.Keywords)}]");
        sb.AppendLine();
        sb.AppendLine("Candidates:");

        foreach (var candidate in candidates)
        {
            var work = candidate.Work;
            var authors = string.Join(", ", work.Authors.Select(a => $"{a.Name} ({a.Role})"));
            sb.AppendLine($"- id: {work.WorkId}");
            sb.AppendLine($"  title: {work.FullTitle}");
            sb.AppendLine($"  authors: {authors}");
            sb.AppendLine($"  firstPublishYear: {work.FirstPublishYear?.ToString() ?? "unknown"}");
            sb.AppendLine($"  matchSignals: {string.Join("; ", candidate.Signals)}");
        }

        sb.AppendLine();
        sb.AppendLine("Respond with ONLY a JSON array: [{\"id\": string, \"explanation\": string}, ...].");
        return sb.ToString();
    }

    private static string Quote(string? value) => value is null ? "null" : $"\"{value}\"";
}
