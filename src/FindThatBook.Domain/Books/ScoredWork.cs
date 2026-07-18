namespace FindThatBook.Domain.Books;

/// <summary>
/// A candidate work paired with the outcome of the ranking engine: which tier it landed in, a continuous score for intra-tier ordering, the human-readable signals
/// that justified the score, and (eventually) an LLM- or rule-generated explanation.
/// </summary>
public sealed class ScoredWork
{
    public required CanonicalWork Work { get; init; }
    public required MatchTier Tier { get; init; }

    /// <summary>Continuous 0..1 score used to break ties within a tier.</summary>
    public required double Score { get; init; }

    /// <summary>Concrete field-level signals, e.g. "Exact title match", "Primary author match".</summary>
    public IReadOnlyList<string> Signals { get; init; } = Array.Empty<string>();

    /// <summary>One- or two-sentence "why this book" explanation. Set after ranking.</summary>
    public string? Explanation { get; set; }

    /// <summary>Composite sort key: tier first (ascending), then score (descending).</summary>
    public (int Tier, double NegScore) SortKey => ((int)Tier, -Score);
}
