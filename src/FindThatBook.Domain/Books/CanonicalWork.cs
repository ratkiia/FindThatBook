namespace FindThatBook.Domain.Books;

/// <summary>
/// A de-duplicated Open Library <em>work</em> (not an edition). Multiple editions and alternate titles ("The Hobbit" / "The Hobbit: There and Back Again") collapse
/// into a single canonical work.
/// </summary>
public sealed class CanonicalWork
{
    public required string WorkKey { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public IReadOnlyList<BookAuthor> Authors { get; init; } = Array.Empty<BookAuthor>();
    public int? FirstPublishYear { get; init; }
    public int? CoverId { get; init; }
    public int EditionCount { get; init; }
    public IReadOnlyList<string> AlternativeTitles { get; init; } = Array.Empty<string>();

    public string? Description { get; set; }

    public IReadOnlyList<string> Subjects { get; set; } = Array.Empty<string>();

    public IEnumerable<BookAuthor> PrimaryAuthors => Authors.Where(a => a.Role == AuthorRole.Primary);
    public IEnumerable<BookAuthor> Contributors => Authors.Where(a => a.Role == AuthorRole.Contributor);

    public BookAuthor? PrimaryAuthor =>PrimaryAuthors.FirstOrDefault() ?? Authors.FirstOrDefault();

    /// <summary>Numeric work id extracted from the key, e.g. "OL27482W".</summary>
    public string WorkId => WorkKey.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? WorkKey;

    public string? CoverUrl => CoverId is { } id? $"https://covers.openlibrary.org/b/id/{id}-M.jpg": null;

    public string OpenLibraryUrl =>
        WorkKey.StartsWith('/') ? $"https://openlibrary.org{WorkKey}" : $"https://openlibrary.org/{WorkKey}";

    public string FullTitle =>string.IsNullOrWhiteSpace(Subtitle) ? Title : $"{Title}: {Subtitle}";
}
