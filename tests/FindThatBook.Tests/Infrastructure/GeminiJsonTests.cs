using FindThatBook.Infrastructure.Gemini;
using FluentAssertions;

namespace FindThatBook.Tests.Infrastructure;

public class GeminiJsonTests
{
    [Fact]
    public void Parses_plain_json()
    {
        using var doc = GeminiJson.Parse("{\"title\":\"The Hobbit\"}");
        doc.RootElement.GetProperty("title").GetString().Should().Be("The Hobbit");
    }

    [Fact]
    public void Strips_markdown_code_fences()
    {
        var text = "```json\n{\"title\":\"The Hobbit\"}\n```";
        using var doc = GeminiJson.Parse(text);
        doc.RootElement.GetProperty("title").GetString().Should().Be("The Hobbit");
    }

    [Fact]
    public void Slices_json_out_of_surrounding_prose()
    {
        var text = "Sure! Here is the result: {\"author\":\"Mark Twain\"} — hope that helps.";
        using var doc = GeminiJson.Parse(text);
        doc.RootElement.GetProperty("author").GetString().Should().Be("Mark Twain");
    }
}
