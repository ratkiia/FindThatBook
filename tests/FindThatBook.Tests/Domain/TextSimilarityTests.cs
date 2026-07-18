using FindThatBook.Domain.Text;
using FluentAssertions;

namespace FindThatBook.Tests.Domain;

public class TextSimilarityTests
{
    [Fact]
    public void Identical_normalized_strings_score_one()
    {
        TextSimilarity.Similarity("The Hobbit", "the hobbit").Should().Be(1d);
    }

    [Fact]
    public void Word_order_and_subtitle_do_not_destroy_similarity()
    {
        // Token-set view keeps this high despite the extra subtitle words.
        TextSimilarity.Similarity("the hobbit", "the hobbit there and back again")
            .Should().BeGreaterThan(0.3d);
    }

    [Fact]
    public void ContainsAllTokens_is_true_when_query_is_a_subset()
    {
        TextSimilarity.ContainsAllTokens("the hobbit", "The Hobbit: There and Back Again")
            .Should().BeTrue();
    }

    [Fact]
    public void ContainsAllTokens_is_false_when_a_token_is_missing()
    {
        TextSimilarity.ContainsAllTokens("the silmarillion", "The Hobbit").Should().BeFalse();
    }

    [Fact]
    public void Unrelated_strings_score_low()
    {
        TextSimilarity.Similarity("the hobbit", "pride and prejudice").Should().BeLessThan(0.3d);
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("book", "book", 0)]
    [InlineData("", "abc", 3)]
    public void Levenshtein_matches_known_values(string a, string b, int expected)
    {
        TextSimilarity.Levenshtein(a, b).Should().Be(expected);
    }
}
