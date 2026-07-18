using FindThatBook.Application.Abstractions;
using FindThatBook.Domain.Books;

namespace FindThatBook.Tests.Support;

/// <summary>
/// In-memory <see cref="IOpenLibraryClient"/> for hermetic tests: returns a fixed set of works for every search and no enrichment details. Records the requests it saw.
/// </summary>
internal sealed class FakeOpenLibraryClient : IOpenLibraryClient
{
    private readonly IReadOnlyList<CanonicalWork> _works;

    public FakeOpenLibraryClient(params CanonicalWork[] works) => _works = works;

    public List<OpenLibrarySearchRequest> Requests { get; } = new();

    public Task<IReadOnlyList<CanonicalWork>> SearchAsync(
        OpenLibrarySearchRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_works);
    }

    public Task<WorkDetails?> GetWorkDetailsAsync(string workKey, CancellationToken cancellationToken) =>
        Task.FromResult<WorkDetails?>(null);
}
