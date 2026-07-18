using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FindThatBook.Infrastructure.Http;

internal static class HttpClientJsonExtensions
{
    /// <summary>
    /// GETs and deserializes JSON, but treats 404 as "no data" (returns default) instead of throwing — Open Library returns 404 for unknown resources.
    /// </summary>
    public static async Task<T?> GetFromJsonWithNotFoundAsync<T>(
        this HttpClient client,
        string requestUri,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(options, cancellationToken);
    }
}
