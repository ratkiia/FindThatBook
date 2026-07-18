using FindThatBook.Domain.Books;

namespace FindThatBook.Application.Abstractions;

/// <summary>
/// Turns a messy free-text blob into structured 
/// </summary>
public interface IAiFieldExtractor
{
    Task<ExtractedFields> ExtractAsync(string rawQuery, CancellationToken cancellationToken);
}
