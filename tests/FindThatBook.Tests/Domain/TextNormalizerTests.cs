using FindThatBook.Domain.Text;
using FluentAssertions;

namespace FindThatBook.Tests.Domain;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Tolkien Hobbit Illustrated Deluxe 1937", "tolkien hobbit illustrated deluxe 1937")]
    [InlineData("  The   Hobbit!!  ", "the hobbit")]
    [InlineData("Tale, of Two Cities.", "tale of two cities")]
    public void Normalize_lowercases_and_strips_punctuation_and_whitespace(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Émile Zola", "emile zola")]
    [InlineData("Gabriel García Márquez", "gabriel garcia marquez")]
    public void Normalize_removes_diacritics(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_handles_null_and_empty()
    {
        TextNormalizer.Normalize(null).Should().BeEmpty();
        TextNormalizer.Normalize("   ").Should().BeEmpty();
    }

    [Fact]
    public void SignificantTokens_drops_stop_words()
    {
        TextNormalizer.SignificantTokens("The Tale of Two Cities")
            .Should().BeEquivalentTo("tale", "two", "cities");
    }
}
