using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FindThatBook.Api.Infrastructure;
/// <summary>
/// Configures Swagger/OpenAPI documentation for the Find That Book API.
/// </summary>
internal static class SwaggerConfig
{
    public static void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Find That Book API",
            Version = "v1",
            Description =
                "Given a messy free-text query, returns an ordered list of likely Open Library " +
                "book matches with grounded, AI-assisted explanations."
        });
    }
}
