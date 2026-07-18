namespace FindThatBook.Application.Search;

public sealed record BookSearchResult
{
    public required string Query { get; init; }

     public required InterpretedQuery Interpreted { get; init; }

    public required IReadOnlyList<BookMatch> Matches { get; init; }

    public bool AiExtractionUsed { get; init; }

    public bool AiExplanationsUsed { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}

public sealed record InterpretedQuery(string? Title, string? Author, IReadOnlyList<string> Keywords);
