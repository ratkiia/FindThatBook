using FindThatBook.Application.Abstractions;
using FindThatBook.Application.Explanations;
using FindThatBook.Application.Ranking;
using FindThatBook.Application.Search;
using FindThatBook.Domain.Books;
using FindThatBook.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using static FindThatBook.Tests.Support.WorkFactory;

namespace FindThatBook.Tests.Application;

public class BookSearchServiceTests
{
    private static BookSearchService CreateService(
        IOpenLibraryClient client,
        IAiFieldExtractor? aiExtractor = null,
        IAiExplanationGenerator? aiExplainer = null)
    {
        var options = Options.Create(new SearchOptions());
        var retriever = new CandidateRetriever(client, options, NullLogger<CandidateRetriever>.Instance);

        return new BookSearchService(
            aiExtractors: aiExtractor is null ? Array.Empty<IAiFieldExtractor>() : new[] { aiExtractor },
            fallbackExtractor: new FallbackFieldExtractor(),
            retriever: retriever,
            ranking: new RankingEngine(),
            aiExplainers: aiExplainer is null ? Array.Empty<IAiExplanationGenerator>() : new[] { aiExplainer },
            explanationComposer: new ExplanationComposer(),
            openLibrary: client,
            options: options,
            logger: NullLogger<BookSearchService>.Instance);
    }

    [Fact]
    public async Task Empty_query_returns_no_matches_with_a_note()
    {
        var service = CreateService(new FakeOpenLibraryClient());

        var result = await service.SearchAsync("   ", CancellationToken.None);

        result.Matches.Should().BeEmpty();
        result.Notes.Should().ContainMatch("*empty*");
    }

    [Fact]
    public async Task End_to_end_twilight_meyer_returns_twilight_by_stephenie_meyer()
    {
        // Brief's E2E example: "twilight meyer" -> Twilight by Stephenie Meyer.
        var twilight = Work("Twilight", "Stephenie Meyer", year: 2005, editionCount: 40);
        var client = new FakeOpenLibraryClient(twilight);

        // AI extracts structured fields for a strong (tier-1) match.
        var aiExtractor = new Mock<IAiFieldExtractor>();
        aiExtractor
            .Setup(x => x.ExtractAsync("twilight meyer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedFields("Twilight", "Stephenie Meyer", Array.Empty<string>()));

        var service = CreateService(client, aiExtractor.Object);

        var result = await service.SearchAsync("twilight meyer", CancellationToken.None);

        result.AiExtractionUsed.Should().BeTrue();
        result.Matches.Should().NotBeEmpty();
        var top = result.Matches[0];
        top.Title.Should().Be("Twilight");
        top.PrimaryAuthor.Should().Be("Stephenie Meyer");
        top.MatchTier.Should().Be(nameof(MatchTier.ExactTitlePrimaryAuthor));
        top.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Falls_back_to_deterministic_path_when_ai_extraction_throws()
    {
        var twilight = Work("Twilight", "Stephenie Meyer", year: 2005);
        var client = new FakeOpenLibraryClient(twilight);

        var failingAi = new Mock<IAiFieldExtractor>();
        failingAi
            .Setup(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Gemini down"));

        var service = CreateService(client, failingAi.Object);

        var result = await service.SearchAsync("twilight meyer", CancellationToken.None);

        result.AiExtractionUsed.Should().BeFalse();
        result.Notes.Should().ContainMatch("*deterministic*");
        result.Matches.Should().NotBeEmpty(); // still returns results via the deterministic path
    }

    [Fact]
    public async Task Uses_rule_based_explanations_when_no_ai_explainer_is_configured()
    {
        var work = Work("Great Expectations", "Charles Dickens", year: 1861);
        var service = CreateService(new FakeOpenLibraryClient(work));

        var result = await service.SearchAsync("dickens great expectations", CancellationToken.None);

        result.AiExplanationsUsed.Should().BeFalse();
        result.Matches.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Explanation));
    }

    [Fact]
    public async Task Runs_all_expected_search_strategies_when_title_and_author_are_known()
    {
        var work = Work("The Hobbit", "J.R.R. Tolkien", year: 1937);
        var client = new FakeOpenLibraryClient(work);

        var aiExtractor = new Mock<IAiFieldExtractor>();
        aiExtractor
            .Setup(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractedFields("The Hobbit", "Tolkien", new[] { "illustrated" }));

        var service = CreateService(client, aiExtractor.Object);
        await service.SearchAsync("tolkien hobbit illustrated", CancellationToken.None);

        // Strategy A (title+author), B (title), C (author), D (free-text) => 4 requests.
        client.Requests.Should().HaveCount(4);
        client.Requests.Should().Contain(r => r.Title != null && r.Author != null);
        client.Requests.Should().Contain(r => r.FreeText != null);
    }
}
