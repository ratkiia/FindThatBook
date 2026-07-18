using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure.Gemini;

/// <summary>
/// Caching decorator over <see cref="GeminiFieldExtractor"/>. Gemini is rate-limited (429 Too Many Requests on the free tier), so identical queries are served from an
/// in-memory cache with a configurable TTL instead of re-calling the model. Only successful results are cached, so a transient failure isn't sticky and the next call gets a fresh chance at the AI path.
/// </summary>
public sealed class CachedAiFieldExtractor : IAiFieldExtractor
{
    private readonly IAiFieldExtractor _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public CachedAiFieldExtractor(IAiFieldExtractor inner, IMemoryCache cache, IOptions<GeminiOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
    }

    public async Task<ExtractedFields> ExtractAsync(string rawQuery, CancellationToken cancellationToken)
    {
        var key = $"ai:extract:{rawQuery.Trim().ToLowerInvariant()}";

        if (_cache.TryGetValue(key, out ExtractedFields? cached) && cached is not null)
        {
            return cached;
        }

        var value = await _inner.ExtractAsync(rawQuery, cancellationToken);

        // Only cache non-empty results so we retry the AI path if it returned nothing.
        if (!value.IsEmpty)
        {
            _cache.Set(key, value, _ttl);
        }

        return value;
    }
}
