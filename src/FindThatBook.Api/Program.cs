using System.Text.Json.Serialization;
using FindThatBook.Api.Infrastructure;
using FindThatBook.Application;
using FindThatBook.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "frontend";

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Emit enums as strings (e.g. MatchTier) and omit nulls for a tidy payload.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(SwaggerConfig.Configure);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks();

// Application + infrastructure (Gemini + Open Library) wiring.
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// CORS for the local React dev server (origins configurable).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// OpenTelemetry tracing across ASP.NET Core and outbound HTTP (Gemini / Open Library).
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("FindThatBook.Api"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        // Show in output window when running locally.
        if (builder.Environment.IsDevelopment())
        {
            tracing.AddConsoleExporter();
        }
    });

var app = builder.Build();

// ---- HTTP pipeline -------------------------------------------------------

// Central exception handling -> ProblemDetails (no stack traces leak to clients).
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Find That Book API v1"));
}

app.UseCors(CorsPolicy);
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.Run();

// Exposed so the integration-test WebApplicationFactory can reference the entry point.
public partial class Program;
