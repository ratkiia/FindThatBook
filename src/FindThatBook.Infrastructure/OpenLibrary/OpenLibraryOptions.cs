namespace FindThatBook.Infrastructure.OpenLibrary;

/// <summary>Configuration for the Open Library gateway.</summary>
public sealed class OpenLibraryOptions
{
    public const string SectionName = "OpenLibrary";

    public string BaseUrl { get; set; } = "https://openlibrary.org";

    /// <summary>
    /// Open Library asks clients to identify themselves via User-Agent so they can contact you about excessive traffic. Set this to your app name + contact.
    /// </summary>
    public string UserAgent { get; set; } = "FindThatBook/1.0 (technical-challenge)";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Cache time-to-live in minutes for search and work-detail responses.</summary>
    public int CacheMinutes { get; set; } = 30;
}
