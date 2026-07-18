using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using FindThatBook.Domain.Text;

namespace FindThatBook.Application.Search;

/// <summary>
/// Deterministic field extractor used when the LLM is unavailable or not configured.
/// </summary>
public sealed class FallbackFieldExtractor : IAiFieldExtractor
{
    public Task<ExtractedFields> ExtractAsync(string rawQuery, CancellationToken cancellationToken)
    {
        var keywords = TextNormalizer.SignificantTokens(rawQuery);
        var fields = keywords.Count == 0
            ? ExtractedFields.Empty
            : new ExtractedFields(Title: null, Author: null, Keywords: keywords);

        return Task.FromResult(fields);
    }
}
