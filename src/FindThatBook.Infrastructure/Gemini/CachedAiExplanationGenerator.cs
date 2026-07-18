using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure.Gemini;

/// <summary>
/// Caching decorator over <see cref="GeminiExplanationGenerator"/>. Explanations are a pure function of the extracted query fields plus the ranked candidate set, so an
/// identical (query, candidates) pair is served from an in-memory cache rather than re-hitting Gemini's rate-limited endpoint. Only non-empty result sets are cached.
/// </summary>
public sealed class CachedAiExplanationGenerator : IAiExplanationGenerator
{
    private readonly IAiExplanationGenerator _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public CachedAiExplanationGenerator(IAiExplanationGenerator inner, IMemoryCache cache, IOptions<GeminiOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
    }

    public async Task<IReadOnlyDictionary<string, string>> ExplainAsync(ExtractedFields query,IReadOnlyList<ScoredWork> candidates,CancellationToken cancellationToken)
    {
        var key = BuildKey(query, candidates);

        if (_cache.TryGetValue(key, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var value = await _inner.ExplainAsync(query, candidates, cancellationToken);

        // Only cache when the model actually produced explanations, so an empty/failed batch isn't remembered and the next call retries the AI path.
        if (value.Count > 0)
        {
            _cache.Set(key, value, _ttl);
        }

        return value;
    }

    // The explanation depends on the query fields and the exact candidate set (work keys, in ranked order). Both go into the key so different result sets don't collide.
    private static string BuildKey(ExtractedFields query, IReadOnlyList<ScoredWork> candidates)
    {
        var fields = $"{query.Title}|{query.Author}|{string.Join(",", query.Keywords)}";
        var works = string.Join(",", candidates.Select(c => c.Work.WorkKey));
        return $"ai:explain:{fields.ToLowerInvariant()}#{works}";
    }
}
