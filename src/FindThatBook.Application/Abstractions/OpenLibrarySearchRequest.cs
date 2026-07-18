namespace FindThatBook.Application.Abstractions;

/// <summary>
/// A single Open Library search request. 
/// </summary>
/// <param name="Title">Maps to title=</param>
/// <param name="Author">Maps to author=</param>
/// <param name="FreeText">Maps to the general q parameter.</param>
/// <param name="Limit">Maximum number of works to return.</param>
public sealed record OpenLibrarySearchRequest(
    string? Title = null,
    string? Author = null,
    string? FreeText = null,
    int Limit = 10)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Title) &&
        string.IsNullOrWhiteSpace(Author) &&
        string.IsNullOrWhiteSpace(FreeText);
}
