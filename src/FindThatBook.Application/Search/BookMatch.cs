using FindThatBook.Domain.Books;

namespace FindThatBook.Application.Search;

public sealed record BookMatch
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    public string? PrimaryAuthor { get; init; }
    public int? FirstPublishYear { get; init; }
    public string? CoverUrl { get; init; }
    public required string OpenLibraryUrl { get; init; }
    public required string WorkId { get; init; }
    public required string Explanation { get; init; }

    /// <summary>The matching-hierarchy tier this candidate landed in.</summary>
    public required string MatchTier { get; init; }

    /// <summary>Continuous 0..1 score for transparency (not a required confidence value).</summary>
    public double Score { get; init; }

    /// <summary>Concrete field-level signals that justified the ranking.</summary>
    public IReadOnlyList<string> Signals { get; init; } = Array.Empty<string>();
}
