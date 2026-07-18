using FindThatBook.Application.Explanations;
using FindThatBook.Application.Ranking;
using FindThatBook.Application.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FindThatBook.Application;

/// <summary>Registers the application-layer services (orchestration, ranking, explanations).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SearchOptions>(configuration.GetSection(SearchOptions.SectionName));

        services.AddSingleton<RankingEngine>();
        services.AddSingleton<ExplanationComposer>();
        services.AddSingleton<FallbackFieldExtractor>();
        services.AddScoped<CandidateRetriever>();
        services.AddScoped<IBookSearchService, BookSearchService>();

        return services;
    }
}
