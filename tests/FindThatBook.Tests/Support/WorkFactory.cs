using FindThatBook.Domain.Books;

namespace FindThatBook.Tests.Support;

/// <summary>Concise helpers for building <see cref="CanonicalWork"/> fixtures in tests.</summary>
internal static class WorkFactory
{
    public static CanonicalWork Work(
        string title,
        string primaryAuthor,
        int? year = null,
        string? subtitle = null,
        int editionCount = 1,
        string workId = "OL1W",
        params string[] contributors)
    {
        var authors = new List<BookAuthor> { new(primaryAuthor, "/authors/OLxA", AuthorRole.Primary) };
        authors.AddRange(contributors.Select(c => new BookAuthor(c, "/authors/OLyA", AuthorRole.Contributor)));

        return new CanonicalWork
        {
            WorkKey = $"/works/{workId}",
            Title = title,
            Subtitle = subtitle,
            Authors = authors,
            FirstPublishYear = year,
            EditionCount = editionCount
        };
    }

    public static ExtractedFields Query(string? title = null, string? author = null, params string[] keywords) =>
        new(title, author, keywords);
}
