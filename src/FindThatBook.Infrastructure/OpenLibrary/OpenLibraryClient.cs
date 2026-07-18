using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;
using FindThatBook.Infrastructure.Http;
using Microsoft.Extensions.Logging;

namespace FindThatBook.Infrastructure.OpenLibrary;

/// <summary>
/// Talks to the Open Library search.json and works/{id}.json endpoints and maps the responses into domain <see cref="CanonicalWork"/> objects.
/// </summary>
public sealed class OpenLibraryClient : IOpenLibraryClient
{
    // Only the fields we actually use — keeps payloads small and fast.
    private const string SearchFields =
        "key,title,subtitle,author_name,author_key,first_publish_year,cover_i,edition_count,alternative_title";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenLibraryClient> _logger;

    public OpenLibraryClient(HttpClient httpClient, ILogger<OpenLibraryClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CanonicalWork>> SearchAsync(
        OpenLibrarySearchRequest request, CancellationToken cancellationToken)
    {
        if (request.IsEmpty)
        {
            return Array.Empty<CanonicalWork>();
        }

        var uri = BuildSearchUri(request);
        var response = await _httpClient.GetFromJsonWithNotFoundAsync<SearchResponse>(uri, SerializerOptions, cancellationToken);

        if (response?.Docs is null || response.Docs.Length == 0)
        {
            return Array.Empty<CanonicalWork>();
        }

        return response.Docs
            .Where(doc => !string.IsNullOrWhiteSpace(doc.Key) && !string.IsNullOrWhiteSpace(doc.Title))
            .Select(MapToCanonicalWork)
            .ToList();
    }

    public async Task<WorkDetails?> GetWorkDetailsAsync(string workKey, CancellationToken cancellationToken)
    {
        var normalizedKey = workKey.StartsWith('/') ? workKey : $"/{workKey}";
        var uri = $"{normalizedKey}.json";

        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var description = ReadDescription(root);
        var subjects = ReadSubjects(root);
        return new WorkDetails(description, subjects);
    }

    private static string BuildSearchUri(OpenLibrarySearchRequest request)
    {
        var query = new StringBuilder("/search.json?");
        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            parameters.Add($"title={Uri.EscapeDataString(request.Title)}");
        }

        if (!string.IsNullOrWhiteSpace(request.Author))
        {
            parameters.Add($"author={Uri.EscapeDataString(request.Author)}");
        }

        if (!string.IsNullOrWhiteSpace(request.FreeText))
        {
            parameters.Add($"q={Uri.EscapeDataString(request.FreeText)}");
        }

        parameters.Add($"fields={SearchFields}");
        parameters.Add($"limit={request.Limit}");

        query.Append(string.Join('&', parameters));
        return query.ToString();
    }

    private static CanonicalWork MapToCanonicalWork(SearchDoc doc)
    {
        var authors = new List<BookAuthor>();
        var names = doc.AuthorNames ?? Array.Empty<string>();
        var keys = doc.AuthorKeys ?? Array.Empty<string>();

        for (var i = 0; i < names.Length; i++)
        {
            var key = i < keys.Length ? $"/authors/{keys[i]}" : null;
            // Heuristic: search.json lists the primary creator first. We treat the first author as Primary and any subsequent names as Contributors so the ranking
            // engine can down-weight contributor-only matches (see README design notes).
            var role = i == 0 ? AuthorRole.Primary : AuthorRole.Contributor;
            authors.Add(new BookAuthor(names[i], key, role));
        }

        return new CanonicalWork
        {
            WorkKey = doc.Key!,
            Title = doc.Title!,
            Subtitle = string.IsNullOrWhiteSpace(doc.Subtitle) ? null : doc.Subtitle,
            Authors = authors,
            FirstPublishYear = doc.FirstPublishYear,
            CoverId = doc.CoverId,
            EditionCount = doc.EditionCount,
            AlternativeTitles = doc.AlternativeTitles ?? Array.Empty<string>()
        };
    }

    private static string? ReadDescription(JsonElement root)
    {
        if (!root.TryGetProperty("description", out var element))
        {
            return null;
        }

        // Open Library returns description either as a plain string or as {type, value}.
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Object when element.TryGetProperty("value", out var value) => value.GetString(),
            _ => null
        };
    }

    private static IReadOnlyList<string> ReadSubjects(JsonElement root)
    {
        if (!root.TryGetProperty("subjects", out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var subjects = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } subject)
            {
                subjects.Add(subject);
            }
        }

        return subjects;
    }

    // ---- search.json response shape ----------------------------------------

    private sealed class SearchResponse
    {
        [JsonPropertyName("docs")]
        public SearchDoc[]? Docs { get; init; }
    }

    private sealed class SearchDoc
    {
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("subtitle")]
        public string? Subtitle { get; init; }

        [JsonPropertyName("author_name")]
        public string[]? AuthorNames { get; init; }

        [JsonPropertyName("author_key")]
        public string[]? AuthorKeys { get; init; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; init; }

        [JsonPropertyName("cover_i")]
        public int? CoverId { get; init; }

        [JsonPropertyName("edition_count")]
        public int EditionCount { get; init; }

        [JsonPropertyName("alternative_title")]
        public string[]? AlternativeTitles { get; init; }
    }
}
