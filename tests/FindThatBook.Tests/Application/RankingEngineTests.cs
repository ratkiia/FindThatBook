using FindThatBook.Application.Ranking;
using FindThatBook.Domain.Books;
using FluentAssertions;
using static FindThatBook.Tests.Support.WorkFactory;

namespace FindThatBook.Tests.Application;

public class RankingEngineTests
{
    private readonly RankingEngine _engine = new();

    [Fact]
    public void Exact_title_and_primary_author_is_tier_one()
    {
        var query = Query(title: "The Hobbit", author: "Tolkien");
        var work = Work("The Hobbit", "J.R.R. Tolkien", year: 1937);

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.ExactTitlePrimaryAuthor);
        result.Signals.Should().Contain(s => s.Contains("primary author"));
    }

    [Fact]
    public void Exact_title_with_contributor_only_author_is_tier_two()
    {
        // Tolkien is the primary author; the query author matches only the illustrator/adaptor.
        var query = Query(title: "The Hobbit", author: "Dixon");
        var work = Work("The Hobbit", "J.R.R. Tolkien", year: 1937, contributors: "Charles Dixon");

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.ExactTitleContributorAuthor);
        result.Signals.Should().Contain(s => s.Contains("contributor"));
    }

    [Fact]
    public void Near_title_with_primary_author_is_tier_three()
    {
        var query = Query(title: "the hobit", author: "Tolkien"); // typo
        var work = Work("The Hobbit", "J.R.R. Tolkien");

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.NearTitlePrimaryAuthor);
    }

    [Fact]
    public void Author_only_query_returns_author_works_as_tier_four()
    {
        var query = Query(author: "dickens");
        var work = Work("Great Expectations", "Charles Dickens");

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.AuthorOnly);
    }

    [Fact]
    public void Keyword_only_query_matches_on_subject_tokens_as_tier_five()
    {
        var query = Query(keywords: "huckleberry");
        var work = Work("Adventures of Huckleberry Finn", "Mark Twain");

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.KeywordOnly);
        result.Signals.Should().Contain(s => s.Contains("keyword"));
    }

    [Fact]
    public void Subtitle_variant_still_counts_as_exact_title()
    {
        var query = Query(title: "The Hobbit", author: "Tolkien");
        var work = Work("The Hobbit", "J.R.R. Tolkien", subtitle: "There and Back Again");

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.ExactTitlePrimaryAuthor);
    }

    [Fact]
    public void Results_are_ordered_by_tier_then_score()
    {
        var query = Query(title: "The Hobbit", author: "Tolkien", keywords: "1937");

        var exactWithYear = Work("The Hobbit", "J.R.R. Tolkien", year: 1937, editionCount: 50, workId: "OL1W");
        var exactWrongYear = Work("The Hobbit", "J.R.R. Tolkien", year: 2001, editionCount: 3, workId: "OL2W");
        var annotated = Work("The Annotated Hobbit", "J.R.R. Tolkien", year: 1988, workId: "OL3W");

        var ranked = _engine.Rank(query, new[] { annotated, exactWrongYear, exactWithYear });

        ranked.Should().HaveCountGreaterThanOrEqualTo(2);
        // The exact-title match whose year also matches should rank first.
        ranked[0].Work.WorkId.Should().Be("OL1W");
        ranked[0].Score.Should().BeGreaterThan(ranked[1].Score);
    }

    [Fact]
    public void Author_partial_name_matches_full_name()
    {
        var query = Query(title: "Twilight", author: "meyer");
        var work = Work("Twilight", "Stephenie Meyer", year: 2005);

        var result = _engine.Rank(query, new[] { work }).Single();

        result.Tier.Should().Be(MatchTier.ExactTitlePrimaryAuthor);
    }

    [Fact]
    public void Completely_unrelated_work_is_excluded()
    {
        var query = Query(title: "The Hobbit", author: "Tolkien");
        var work = Work("Pride and Prejudice", "Jane Austen");

        var ranked = _engine.Rank(query, new[] { work });

        ranked.Should().BeEmpty();
    }
}
