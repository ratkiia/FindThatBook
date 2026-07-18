using FindThatBook.Application.Abstractions;
using FindThatBook.Application.Explanations;
using FindThatBook.Application.Ranking;
using FindThatBook.Domain.Books;
using FindThatBook.Domain.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FindThatBook.Application.Search;

/// <summary>
/// The search orchestrator. Coordinates the full pipeline described in the requirement:
/// normalize -> extract (AI, with deterministic fallback) -> retrieve candidates -> canonical de-dup -> rank -> enrich -> explain (AI, with rule-based fallback).
/// Every AI step degrades gracefully: if the LLM is missing or fails, the pipeline still returns deterministically-ranked results.
/// </summary>
public sealed class BookSearchService : IBookSearchService
{
    private readonly IAiFieldExtractor? _aiExtractor;
    private readonly FallbackFieldExtractor _fallbackExtractor;
    private readonly CandidateRetriever _retriever;
    private readonly RankingEngine _ranking;
    private readonly IAiExplanationGenerator? _aiExplainer;
    private readonly ExplanationComposer _explanationComposer;
    private readonly IOpenLibraryClient _openLibrary;
    private readonly SearchOptions _options;
    private readonly ILogger<BookSearchService> _logger;

    public BookSearchService(
        IEnumerable<IAiFieldExtractor> aiExtractors,
        FallbackFieldExtractor fallbackExtractor,
        CandidateRetriever retriever,
        RankingEngine ranking,
        IEnumerable<IAiExplanationGenerator> aiExplainers,
        ExplanationComposer explanationComposer,
        IOpenLibraryClient openLibrary,
        IOptions<SearchOptions> options,
        ILogger<BookSearchService> logger)
    {
        // AI services are optional: if Gemini is not configured they simply are not registered.
        _aiExtractor = aiExtractors.FirstOrDefault();
        _aiExplainer = aiExplainers.FirstOrDefault();
        _fallbackExtractor = fallbackExtractor;
        _retriever = retriever;
        _ranking = ranking;
        _explanationComposer = explanationComposer;
        _openLibrary = openLibrary;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BookSearchResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var raw = (query ?? string.Empty).Trim();
        var notes = new List<string>();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new BookSearchResult
            {
                Query = raw,
                Interpreted = new InterpretedQuery(null, null, Array.Empty<string>()),
                Matches = Array.Empty<BookMatch>(),
                Notes = new[] { "Query was empty." }
            };
        }

        var normalized = TextNormalizer.Normalize(raw);

        // 1. Field extraction (AI first, deterministic fallback).
        var (fields, aiExtractionUsed) = await ExtractFieldsAsync(raw, notes, cancellationToken);

        // 2. Candidate retrieval (multi-strategy).
        var candidates = await _retriever.RetrieveAsync(fields, normalized, cancellationToken);
        if (candidates.Count == 0)
        {
            notes.Add("No candidates were found on Open Library for this query.");
            return BuildResult(raw, fields, Array.Empty<ScoredWork>(), aiExtractionUsed, false, notes);
        }

        // 3 & 4. Rank (canonical de-dup already happened during retrieval).
        var ranked = _ranking.Rank(fields, candidates);
        if (ranked.Count == 0)
        {
            notes.Add("Found candidates but none matched confidently; showing nothing rather than a wrong guess.");
            return BuildResult(raw, fields, Array.Empty<ScoredWork>(), aiExtractionUsed, false, notes);
        }

        var top = ranked.Take(_options.MaxResults).ToList();

        // 5. Enrich the top matches, then re-rank so subject/description signals count.
        await EnrichAsync(top, cancellationToken);
        top = _ranking.Rank(fields, top.Select(t => t.Work).ToList())
            .Take(_options.MaxResults)
            .ToList();

        // 6. Explanations (AI first, rule-based fallback).
        var aiExplanationsUsed = await ApplyExplanationsAsync(fields, top, notes, cancellationToken);

        return BuildResult(raw, fields, top, aiExtractionUsed, aiExplanationsUsed, notes);
    }

    private async Task<(ExtractedFields Fields, bool AiUsed)> ExtractFieldsAsync(
        string raw, List<string> notes, CancellationToken cancellationToken)
    {
        if (_aiExtractor is null)
        {
            notes.Add("AI extraction not configured; used deterministic keyword extraction.");
            return (await _fallbackExtractor.ExtractAsync(raw, cancellationToken), false);
        }

        try
        {
            var fields = await _aiExtractor.ExtractAsync(raw, cancellationToken);
            if (fields.IsEmpty)
            {
                notes.Add("AI returned no fields; used deterministic keyword extraction.");
                return (await _fallbackExtractor.ExtractAsync(raw, cancellationToken), false);
            }

            return (fields, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI field extraction failed; falling back to deterministic extraction");
            notes.Add("AI extraction unavailable; used deterministic keyword extraction.");
            return (await _fallbackExtractor.ExtractAsync(raw, cancellationToken), false);
        }
    }

    private async Task EnrichAsync(IReadOnlyList<ScoredWork> top, CancellationToken cancellationToken)
    {
        var toEnrich = top.Take(_options.EnrichTopN).ToList();

        var enrichments = toEnrich.Select(async scored =>
        {
            try
            {
                var details = await _openLibrary.GetWorkDetailsAsync(scored.Work.WorkKey, cancellationToken);
                if (details is not null)
                {
                    scored.Work.Description = details.Description;
                    scored.Work.Subjects = details.Subjects;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Enrichment failed for {WorkKey}", scored.Work.WorkKey);
            }
        });

        await Task.WhenAll(enrichments);
    }

    private async Task<bool> ApplyExplanationsAsync(
        ExtractedFields fields, IReadOnlyList<ScoredWork> top, List<string> notes, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> aiExplanations =
            new Dictionary<string, string>();

        if (_aiExplainer is not null)
        {
            try
            {
                aiExplanations = await _aiExplainer.ExplainAsync(fields, top, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI explanation generation failed; falling back to rule-based explanations");
                notes.Add("AI explanations unavailable; used rule-based explanations.");
            }
        }

        var anyAi = false;
        foreach (var scored in top)
        {
            if (aiExplanations.TryGetValue(scored.Work.WorkKey, out var explanation) &&
                !string.IsNullOrWhiteSpace(explanation))
            {
                scored.Explanation = explanation.Trim();
                anyAi = true;
            }
            else
            {
                scored.Explanation = _explanationComposer.Compose(fields, scored);
            }
        }

        return anyAi;
    }

    private static BookSearchResult BuildResult(
        string query,
        ExtractedFields fields,
        IReadOnlyList<ScoredWork> ranked,
        bool aiExtractionUsed,
        bool aiExplanationsUsed,
        IReadOnlyList<string> notes)
    {
        var matches = ranked.Select(ToMatch).ToList();

        return new BookSearchResult
        {
            Query = query,
            Interpreted = new InterpretedQuery(fields.Title, fields.Author, fields.Keywords),
            Matches = matches,
            AiExtractionUsed = aiExtractionUsed,
            AiExplanationsUsed = aiExplanationsUsed,
            Notes = notes
        };
    }

    private static BookMatch ToMatch(ScoredWork scored)
    {
        var work = scored.Work;
        return new BookMatch
        {
            Title = work.Title,
            Subtitle = work.Subtitle,
            Authors = work.Authors.Select(a => a.Name).ToList(),
            PrimaryAuthor = work.PrimaryAuthor?.Name,
            FirstPublishYear = work.FirstPublishYear,
            CoverUrl = work.CoverUrl,
            OpenLibraryUrl = work.OpenLibraryUrl,
            WorkId = work.WorkId,
            Explanation = scored.Explanation ?? string.Empty,
            MatchTier = scored.Tier.ToString(),
            Score = scored.Score,
            Signals = scored.Signals
        };
    }
}
