using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure.Gemini;

/// <summary>
/// Thin transport wrapper over the Gemini <c>generateContent</c> endpoint.
/// </summary>
public sealed class GeminiApi
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiApi> _logger;

    public GeminiApi(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiApi> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends a prompt and returns the model's raw text response, requesting a JSON MIME type so the model is nudged toward machine-readable output.
    /// </summary>
    public async Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken)
    {
        var request = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent { Parts = new[] { new GeminiPart { Text = prompt } } }
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0,
                ResponseMimeType = "application/json"
            }
        };

        // The API key is passed as a query-string parameter per the Gemini docs.
        var relativeUri = $"v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        using var response = await _httpClient.PostAsJsonAsync(relativeUri, request, SerializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GeminiResponse>(SerializerOptions, cancellationToken);
        var text = payload?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Gemini returned an empty completion");
            throw new InvalidOperationException("Gemini returned an empty completion.");
        }

        return text;
    }

    // ---- Request/response envelope -----------------------------------------

    private sealed class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public GeminiContent[] Contents { get; init; } = Array.Empty<GeminiContent>();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig? GenerationConfig { get; init; }
    }

    private sealed class GeminiContent
    {
        [JsonPropertyName("parts")]
        public GeminiPart[] Parts { get; init; } = Array.Empty<GeminiPart>();
    }

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; init; }

        [JsonPropertyName("responseMimeType")]
        public string? ResponseMimeType { get; init; }
    }

    private sealed class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public GeminiCandidate[]? Candidates { get; init; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; init; }
    }
}
