using FindThatBook.Application.Search;
using FluentAssertions;

namespace FindThatBook.Tests.Application;

public class FallbackFieldExtractorTests
{
    private readonly FallbackFieldExtractor _extractor = new();

    [Fact]
    public async Task Extracts_significant_tokens_as_keywords()
    {
        var fields = await _extractor.ExtractAsync("Tolkien Hobbit Illustrated 1937", CancellationToken.None);

        fields.Title.Should().BeNull();
        fields.Author.Should().BeNull();
        fields.Keywords.Should().BeEquivalentTo("tolkien", "hobbit", "illustrated", "1937");
    }

    [Fact]
    public async Task Empty_query_yields_empty_fields()
    {
        var fields = await _extractor.ExtractAsync("   ", CancellationToken.None);
        fields.IsEmpty.Should().BeTrue();
    }
}
