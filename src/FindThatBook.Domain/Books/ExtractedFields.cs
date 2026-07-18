namespace FindThatBook.Domain.Books;
/// <param name="Title">Best-guess book title, if one could be identified.</param>
/// <param name="Author">Best-guess author name, if one could be identified.</param>
/// <param name="Keywords">Remaining meaningful tokens (character names, adjectives, years).</param>
public sealed record ExtractedFields(string? Title,string? Author,IReadOnlyList<string> Keywords)
{
    public static ExtractedFields Empty { get; } = new(null, null, Array.Empty<string>());

    public bool IsEmpty =>string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Author) && Keywords.Count == 0;

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);
}
