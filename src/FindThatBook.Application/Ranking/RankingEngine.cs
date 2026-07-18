using FindThatBook.Domain.Books;
using FindThatBook.Domain.Text;

namespace FindThatBook.Application.Ranking;

/// <summary>
/// Deterministic scoring engine. Given the interpreted query and a set of candidate works, it assigns each candidate a (the matching hierarchy from the brief) and a continuous score used to order candidates within a tier.
/// This is intentionally rule-based and side-effect free: it is fully unit-testable and gives the pipeline a reliable ranking even when the LLM is unavailable.
/// </summary>
public sealed class RankingEngine
{
    // counts as an exact/normalized match.
    private const double ExactTitleThreshold = 0.90;

    // counts as a near match.
    private const double NearTitleThreshold = 0.55;

    // counts as an author match.
    private const double AuthorMatchThreshold = 0.80;

    public IReadOnlyList<ScoredWork> Rank(ExtractedFields query, IReadOnlyList<CanonicalWork> works)
    {
        var scored = works
            .Select(work => Evaluate(query, work))
            .Where(s => s.Tier != MatchTier.NoMatch)
            .OrderBy(s => s.SortKey.Tier)
            .ThenBy(s => s.SortKey.NegScore)
            .ToList();

        return scored;
    }

    private static ScoredWork Evaluate(ExtractedFields query, CanonicalWork work)
    {
        var signals = new List<string>();

        // ---- Title signal -------------------------------------------------
        var titleSim = 0d;
        if (query.HasTitle)
        {
            var titleCandidates = new List<string> { work.Title, work.FullTitle };
            titleCandidates.AddRange(work.AlternativeTitles);
            titleSim = titleCandidates.Max(t => TextSimilarity.Similarity(query.Title, t));

            if (titleSim < 0.95 && TextSimilarity.ContainsAllTokens(query.Title, work.FullTitle))
            {
                titleSim = Math.Max(titleSim, 0.95);
            }
        }

        var exactTitle = query.HasTitle && titleSim >= ExactTitleThreshold;
        var nearTitle = query.HasTitle && titleSim >= NearTitleThreshold;

        // ---- Author signal ------------------------------------------------
        var bestPrimarySim = 0d;
        var bestContributorSim = 0d;
        BookAuthor? matchedPrimary = null;
        BookAuthor? matchedContributor = null;

        if (query.HasAuthor)
        {
            foreach (var author in work.Authors)
            {
                var sim = AuthorSimilarity(query.Author!, author.Name);
                if (author.Role == AuthorRole.Primary && sim > bestPrimarySim)
                {
                    bestPrimarySim = sim;
                    matchedPrimary = author;
                }
                else if (author.Role == AuthorRole.Contributor && sim > bestContributorSim)
                {
                    bestContributorSim = sim;
                    matchedContributor = author;
                }
            }
        }

        var primaryAuthorMatch = bestPrimarySim >= AuthorMatchThreshold;
        var contributorMatch = bestContributorSim >= AuthorMatchThreshold;

        var queryYear = ExtractYear(query.Keywords);
        var yearMatch = queryYear.HasValue && work.FirstPublishYear == queryYear;

        var (keywordRelevance, matchedKeywords) = ScoreKeywords(query.Keywords, work);

        var tier = DecideTier(query, exactTitle, nearTitle, primaryAuthorMatch, contributorMatch, keywordRelevance);

        if (exactTitle)
        {
            signals.Add("Exact/normalized title match");
        }
        else if (nearTitle)
        {
            signals.Add($"Close title match (~{titleSim:P0})");
        }

        if (primaryAuthorMatch && matchedPrimary is not null)
        {
            signals.Add($"{matchedPrimary.Name} is the primary author");
        }
        else if (contributorMatch && matchedContributor is not null)
        {
            signals.Add($"{matchedContributor.Name} is listed as a contributor, not the primary author");
        }

        if (tier == MatchTier.AuthorOnly)
        {
            signals.Add("Returned as a top work by the matched author");
        }

        if (yearMatch)
        {
            signals.Add($"First published {work.FirstPublishYear} matches the query");
        }

        if (matchedKeywords.Count > 0)
        {
            signals.Add($"Matches keyword(s): {string.Join(", ", matchedKeywords)}");
        }

        var score = ComputeScore(query, titleSim, bestPrimarySim, bestContributorSim,
            primaryAuthorMatch, yearMatch, keywordRelevance, work.EditionCount);

        return new ScoredWork
        {
            Work = work,
            Tier = tier,
            Score = Math.Round(score, 4),
            Signals = signals
        };
    }

    private static MatchTier DecideTier(
        ExtractedFields query,
        bool exactTitle,
        bool nearTitle,
        bool primaryAuthorMatch,
        bool contributorMatch,
        double keywordRelevance)
    {
        if (exactTitle && primaryAuthorMatch)
        {
            return MatchTier.ExactTitlePrimaryAuthor;
        }

        if (exactTitle && contributorMatch)
        {
            return MatchTier.ExactTitleContributorAuthor;
        }

        if (nearTitle && primaryAuthorMatch)
        {
            return MatchTier.NearTitlePrimaryAuthor;
        }

        if (!query.HasAuthor && exactTitle)
        {
            return MatchTier.NearTitlePrimaryAuthor;
        }

        if (query.HasAuthor && primaryAuthorMatch)
        {
            return MatchTier.AuthorOnly;
        }

        if (!query.HasAuthor && nearTitle)
        {
            return MatchTier.KeywordOnly;
        }

        if (query.HasAuthor && contributorMatch)
        {
            return MatchTier.KeywordOnly;
        }

        if (keywordRelevance > 0)
        {
            return MatchTier.KeywordOnly;
        }

        return MatchTier.NoMatch;
    }

    private static double ComputeScore(
        ExtractedFields query,
        double titleSim,
        double primarySim,
        double contributorSim,
        bool primaryAuthorMatch,
        bool yearMatch,
        double keywordRelevance,
        int editionCount)
    {
        double weightedSum = 0;
        double weight = 0;

        if (query.HasTitle)
        {
            weightedSum += titleSim * 0.6;
            weight += 0.6;
        }

        if (query.HasAuthor)
        {
            var authorSim = Math.Max(primarySim, contributorSim * 0.7);
            weightedSum += authorSim * 0.4;
            weight += 0.4;
        }

        if (!query.HasTitle && !query.HasAuthor)
        {
            weightedSum += keywordRelevance;
            weight += 1;
        }

        var baseSimilarity = weight > 0 ? weightedSum / weight : 0;
        var score = baseSimilarity * 0.80;

        if (primaryAuthorMatch)
        {
            score += 0.06;
        }

        if (yearMatch)
        {
            score += 0.06;
        }

        if (query.HasTitle || query.HasAuthor)
        {
            score += keywordRelevance * 0.04;
        }

        score += Math.Min(editionCount, 60) / 60d * 0.04;

        return Math.Clamp(score, 0d, 1d);
    }

    /// <summary>
    /// Rewards the fraction of query tokens that strongly match some token of the candidate name.
    /// </summary>
    private static double AuthorSimilarity(string queryAuthor, string candidateName)
    {
        var queryTokens = TextNormalizer.SignificantTokens(queryAuthor);
        var nameTokens = TextNormalizer.SignificantTokens(candidateName);

        if (queryTokens.Count == 0 || nameTokens.Count == 0)
        {
            return 0d;
        }

        var matched = 0;
        foreach (var token in queryTokens)
        {
            var best = nameTokens.Max(n => TextSimilarity.EditSimilarity(token, n));
            if (best >= 0.85)
            {
                matched++;
            }
        }

        var coverage = (double)matched / queryTokens.Count;
        var overall = TextSimilarity.Similarity(queryAuthor, candidateName);
        return Math.Max(coverage, overall);
    }

    private static (double Relevance, IReadOnlyList<string> Matched) ScoreKeywords(
        IReadOnlyList<string> keywords, CanonicalWork work)
    {
        var significant = keywords
            .SelectMany(TextNormalizer.SignificantTokens)
            .Where(t => !int.TryParse(t, out _)) // years are handled separately
            .Distinct()
            .ToList();

        if (significant.Count == 0)
        {
            return (0d, Array.Empty<string>());
        }

        var haystack = new HashSet<string>();
        AddTokens(haystack, work.FullTitle);
        foreach (var author in work.Authors)
        {
            AddTokens(haystack, author.Name);
        }

        foreach (var subject in work.Subjects)
        {
            AddTokens(haystack, subject);
        }

        if (!string.IsNullOrWhiteSpace(work.Description))
        {
            AddTokens(haystack, work.Description);
        }

        var matched = significant.Where(haystack.Contains).ToList();
        return ((double)matched.Count / significant.Count, matched);
    }

    private static void AddTokens(HashSet<string> set, string text)
    {
        foreach (var token in TextNormalizer.Tokenize(text))
        {
            set.Add(token);
        }
    }

    private static int? ExtractYear(IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            foreach (var token in TextNormalizer.Tokenize(keyword))
            {
                if (token.Length == 4 && int.TryParse(token, out var year) && year is >= 1400 and <= 2100)
                {
                    return year;
                }
            }
        }

        return null;
    }
}
