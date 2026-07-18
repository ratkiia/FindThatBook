using FindThatBook.Domain.Books;

namespace FindThatBook.Application.Abstractions;

/// <summary>
/// Produces a short, grounded "why this book" explanation for each ranked candidate.
/// The generator is given the deterministic signals so the LLM stays factual rather
/// than inventing details.
/// </summary>
public interface IAiExplanationGenerator
{
    /// <summary>
    /// Returns explanations keyed by <see cref="CanonicalWork.WorkKey"/>. A missing
    /// key means the caller should fall back to a rule-based explanation.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ExplainAsync(
        ExtractedFields query,
        IReadOnlyList<ScoredWork> candidates,
        CancellationToken cancellationToken);
}
