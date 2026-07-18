namespace FindThatBook.Domain.Books;

/// <summary>
/// A person associated with the book.
/// </summary>
/// <param name="Name">Display name, e.g. "J.R.R. Tolkien".</param>
/// <param name="Key">Open Library author key, e.g. "/authors/OL26320A" (optional).</param>
/// <param name="Role">Whether this person is a primary author or a contributor.</param>
public sealed record BookAuthor(string Name, string? Key = null, AuthorRole Role = AuthorRole.Primary);
