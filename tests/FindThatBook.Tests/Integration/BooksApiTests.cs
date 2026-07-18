using System.Net;
using System.Net.Http.Json;
using FindThatBook.Application.Abstractions;
using FindThatBook.Application.Search;
using FindThatBook.Tests.Support;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FindThatBook.Tests.Integration;

/// <summary>
/// Boots the real API through WebApplicationFactory but swaps the Open Library gateway for an in-memory fake so the tests are fast and network-independent.
/// </summary>
public class BooksApiTests : IClassFixture<BooksApiTests.Factory>
{
    private readonly Factory _factory;

    public BooksApiTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Search_returns_200_and_ranked_matches()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/books/search", new { query = "twilight meyer" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BookSearchResult>();
        result.Should().NotBeNull();
        result!.Matches.Should().NotBeEmpty();
        result.Matches[0].Title.Should().Be("Twilight");
    }

    [Fact]
    public async Task Search_with_empty_query_returns_400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/books/search", new { query = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Healthy");
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOpenLibraryClient>();
                var twilight = WorkFactory.Work("Twilight", "Stephenie Meyer", year: 2005, editionCount: 40);
                services.AddSingleton<IOpenLibraryClient>(new FakeOpenLibraryClient(twilight));
            });
        }
    }
}
