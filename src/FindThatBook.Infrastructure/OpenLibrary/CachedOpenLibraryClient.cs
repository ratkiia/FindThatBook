using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure.OpenLibrary;

/// <summary>
/// Caching decorator over <see cref="OpenLibraryClient"/>. Open Library is a shared,rate-limited public API, so identical searches and work look-ups are served from an in-memory cache with a configurable TTL. This both speeds up repeat queries and
/// keeps us well within Open Library's fair-use expectations.
/// </summary>
public sealed class CachedOpenLibraryClient : IOpenLibraryClient
{
    private readonly OpenLibraryClient _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public CachedOpenLibraryClient(OpenLibraryClient inner, IMemoryCache cache, IOptions<OpenLibraryOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.CacheMinutes));
    }

    public Task<IReadOnlyList<CanonicalWork>> SearchAsync(
        OpenLibrarySearchRequest request, CancellationToken cancellationToken)
    {
        var key = $"ol:search:{request.Title}|{request.Author}|{request.FreeText}|{request.Limit}";
        return GetOrCreateAsync(key, ct => _inner.SearchAsync(request, ct), cancellationToken);
    }

    public Task<WorkDetails?> GetWorkDetailsAsync(string workKey, CancellationToken cancellationToken)
    {
        var key = $"ol:work:{workKey}";
        return GetOrCreateAsync(key, ct => _inner.GetWorkDetailsAsync(workKey, ct), cancellationToken);
    }

    private async Task<T> GetOrCreateAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);

        // Only cache successful, non-null results so transient failures aren't sticky.
        if (value is not null)
        {
            _cache.Set(key, value, _ttl);
        }

        return value;
    }
}
