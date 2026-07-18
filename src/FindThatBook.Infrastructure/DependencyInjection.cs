using FindThatBook.Application.Abstractions;
using FindThatBook.Infrastructure.Gemini;
using FindThatBook.Infrastructure.OpenLibrary;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure;

/// <summary>
/// Registers the infrastructure adapters: Open Library gateway (cached + resilient)
/// and, when configured, the Gemini AI services. Everything is wired behind the
/// application-layer interfaces so the domain never sees HTTP or JSON.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.Configure<OpenLibraryOptions>(configuration.GetSection(OpenLibraryOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

        AddOpenLibrary(services);
        AddGeminiIfConfigured(services, configuration);

        return services;
    }

    private static void AddOpenLibrary(IServiceCollection services)
    {
        services.AddHttpClient<OpenLibraryClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<OpenLibraryOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                client.Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 30));
            })
            .AddStandardResilienceHandler();

        // The public-facing gateway is the caching decorator over the raw client.
        services.AddScoped<IOpenLibraryClient>(sp => new CachedOpenLibraryClient(
            sp.GetRequiredService<OpenLibraryClient>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
            sp.GetRequiredService<IOptions<OpenLibraryOptions>>()));
    }

    private static void AddGeminiIfConfigured(IServiceCollection services, IConfiguration configuration)
    {
        var geminiOptions = new GeminiOptions();
        configuration.GetSection(GeminiOptions.SectionName).Bind(geminiOptions);

        if (!geminiOptions.IsConfigured)
        {
            // No API key -> AI services are intentionally not registered. The application
            // layer detects their absence and uses the deterministic fallback path.
            return;
        }

        services.AddHttpClient<GeminiApi>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 30));
            })
            .AddStandardResilienceHandler();

        // Concrete Gemini adapters, wrapped in caching decorators so identical queries
        // are served from memory instead of re-hitting Gemini's rate-limited endpoint.
        services.AddScoped<GeminiFieldExtractor>();
        services.AddScoped<IAiFieldExtractor>(sp => new CachedAiFieldExtractor(
            sp.GetRequiredService<GeminiFieldExtractor>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<IOptions<GeminiOptions>>()));

        services.AddScoped<GeminiExplanationGenerator>();
        services.AddScoped<IAiExplanationGenerator>(sp => new CachedAiExplanationGenerator(
            sp.GetRequiredService<GeminiExplanationGenerator>(),
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<IOptions<GeminiOptions>>()));
    }
}
