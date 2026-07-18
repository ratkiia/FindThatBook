namespace FindThatBook.Infrastructure.Gemini;

/// <summary>Configuration for the Google Gemini integration.</summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>API key from https://ai.google.dev/gemini-api/docs/api-key. When empty the AI path is disabled.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model id. Defaults to a fast, free-tier-friendly model.</summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com";

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// How long to cache AI results (extraction + explanations) per identical query.
    /// Repeated searches are served from memory so we don't re-hit Gemini's rate limits.
    /// </summary>
    public int CacheMinutes { get; set; } = 360;

    /// <summary>Master switch. Even with a key present the AI path can be turned off.</summary>
    public bool Enabled { get; set; } = true;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
