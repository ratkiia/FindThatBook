namespace FindThatBook.Application.Search;

/// <summary>
/// The application's single entry point: As per requirment it takes a messy query string and return an ordered list of likely book matches with grounded explanations.
/// </summary>
public interface IBookSearchService
{
    Task<BookSearchResult> SearchAsync(string query, CancellationToken cancellationToken);
}
