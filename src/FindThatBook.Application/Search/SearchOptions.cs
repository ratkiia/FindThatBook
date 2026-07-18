namespace FindThatBook.Application.Search;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    /// <summary>Maximum number of ranked matches returned to the caller</summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>Results requested per individual Open Library strategy.</summary>
    public int PerStrategyLimit { get; set; } = 10;

    /// <summary>Upper bound on the merged candidate pool before ranking.</summary>
    public int CandidatePoolSize { get; set; } = 30;

    /// <summary>How many of the top ranked works to enrich via /works/{id}.json for grounding.</summary>
    public int EnrichTopN { get; set; } = 5;
}
