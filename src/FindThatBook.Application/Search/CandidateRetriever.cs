using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FindThatBook.Application.Search;

/// <summary>
/// Perform the multi-strategy Open Library retrieval described in the requirement and merges the results into a single de-duplicated candidate pool.
/// Strategy A: title + author   (most precise)
/// Strategy B: title only
/// Strategy C: author only      (author-only fallback)
/// Strategy D: free-text q=      (keyword fallback — always runs, drives the no-AI path)
/// Strategies run concurrently. A failure in one does not sink the others.
/// </summary>
public sealed class CandidateRetriever
{
    private readonly IOpenLibraryClient _client;
    private readonly SearchOptions _options;
    private readonly ILogger<CandidateRetriever> _logger;

    public CandidateRetriever(
        IOpenLibraryClient client,
        IOptions<SearchOptions> options,
        ILogger<CandidateRetriever> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }
    // Retrieve the books details from the openLibrary
    public async Task<IReadOnlyList<CanonicalWork>> RetrieveAsync(
        ExtractedFields fields,
        string freeTextQuery,
        CancellationToken cancellationToken)
    {
        var requests = BuildRequests(fields, freeTextQuery);

        var searches = requests.Select(request => SafeSearchAsync(request, cancellationToken));
        var resultSets = await Task.WhenAll(searches);

        var merged = new Dictionary<string, CanonicalWork>(StringComparer.OrdinalIgnoreCase);
        foreach (var work in resultSets.SelectMany(set => set))
        {
            merged.TryAdd(work.WorkKey, work);
            if (merged.Count >= _options.CandidatePoolSize)
            {
                break;
            }
        }

        _logger.LogInformation( "Retrieved {Count} unique candidates from {Strategies} strategies", merged.Count, requests.Count);

        return merged.Values.ToList();
    }

    private List<OpenLibrarySearchRequest> BuildRequests(ExtractedFields fields, string freeTextQuery)
    {
        var limit = _options.PerStrategyLimit;
        var requests = new List<OpenLibrarySearchRequest>();

        // Strategy A: title + author
        if (fields.HasTitle && fields.HasAuthor)
        {
            requests.Add(new OpenLibrarySearchRequest(Title: fields.Title, Author: fields.Author, Limit: limit));
        }

        // Strategy B: title only
        if (fields.HasTitle)
        {
            requests.Add(new OpenLibrarySearchRequest(Title: fields.Title, Limit: limit));
        }

        // Strategy C: author only
        if (fields.HasAuthor)
        {
            requests.Add(new OpenLibrarySearchRequest(Author: fields.Author, Limit: limit));
        }

        // Strategy D: free-text keyword fallback — always run so the no-AI path works.
        var freeText = BuildFreeText(fields, freeTextQuery);
        if (!string.IsNullOrWhiteSpace(freeText))
        {
            requests.Add(new OpenLibrarySearchRequest(FreeText: freeText, Limit: limit));
        }

        return requests;
    }

    private static string BuildFreeText(ExtractedFields fields, string freeTextQuery)
    {
        // Prefer the structured fields when present, otherwise the raw normalized query.
        var parts = new List<string>();
        if (fields.HasTitle)
        {
            parts.Add(fields.Title!);
        }

        if (fields.HasAuthor)
        {
            parts.Add(fields.Author!);
        }

        parts.AddRange(fields.Keywords);

        var combined = string.Join(' ', parts).Trim();
        return string.IsNullOrWhiteSpace(combined) ? freeTextQuery : combined;
    }

    private async Task<IReadOnlyList<CanonicalWork>> SafeSearchAsync(
        OpenLibrarySearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SearchAsync(request, cancellationToken); // getting the result directly from the Openlibrary
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open Library search strategy failed: {Request}", request);
            return Array.Empty<CanonicalWork>();
        }
    }
}
