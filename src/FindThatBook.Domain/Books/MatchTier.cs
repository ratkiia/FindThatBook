namespace FindThatBook.Domain.Books;

/// <summary>
/// The matching hierarchy described in the requirement. Lower numeric values are stronger matches. 
/// </summary>
public enum MatchTier
{
    /// <summary>Exact/normalized title match with the primary author (strongest).</summary>
    ExactTitlePrimaryAuthor = 1,

    /// <summary>Exact/normalized title match, but the author is only a contributor.</summary>
    ExactTitleContributorAuthor = 2,

    /// <summary>Near (fuzzy) title match with the primary author.</summary>
    NearTitlePrimaryAuthor = 3,

    /// <summary>Matched on author only (e.g. "dickens") — return top works by author.</summary>
    AuthorOnly = 4,

    /// <summary>Matched on keywords/character hints only (weakest).</summary>
    KeywordOnly = 5,

    /// <summary>No meaningful match.</summary>
    NoMatch = 99
}
