namespace FindThatBook.Domain.Books;

/// <summary>
/// Distinguishes the creator of a work from people who contributed .
/// </summary>
public enum AuthorRole
{
    Primary = 0,

    Contributor = 1
}
