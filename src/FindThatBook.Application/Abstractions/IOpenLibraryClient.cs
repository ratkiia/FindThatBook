using FindThatBook.Domain.Books;

namespace FindThatBook.Application.Abstractions;

/// <summary>
/// Gateway to the Open Library APIs. 
/// </summary>
public interface IOpenLibraryClient
{
    /// <summary>
    /// Runs a search.json query and returns de-duplicated canonical works.
     /// </summary>
    Task<IReadOnlyList<CanonicalWork>> SearchAsync(OpenLibrarySearchRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches /works/{id}.json detail used to ground explanations. Returns "null" when the work is unavailable; callers must degrade gracefully.
    /// </summary>
    Task<WorkDetails?> GetWorkDetailsAsync(string workKey, CancellationToken cancellationToken);
}
