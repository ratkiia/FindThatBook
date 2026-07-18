using FindThatBook.Api.Models;
using FindThatBook.Application.Search;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Controllers;

[ApiController]
[Route("api/books")]
[Produces("application/json")]
public sealed class BooksController : ControllerBase
{
    private readonly IBookSearchService _searchService;
    private readonly ILogger<BooksController> _logger;

    public BooksController(IBookSearchService searchService, ILogger<BooksController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Finds the most likely book(s) for a messy free-text query, returning an ordered
    /// list of candidates each with a grounded "why this book" explanation.
    /// </summary>
    /// <response code="200">Search completed. Matches may be empty if nothing matched confidently.</response>
    /// <response code="400">The query was missing or invalid.</response>
    [HttpPost("search")]
    [ProducesResponseType(typeof(BookSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookSearchResult>> Search(
        [FromBody] SearchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Book search requested for query {Query}", request.Query);

        var result = await _searchService.SearchAsync(request.Query, cancellationToken);
        return Ok(result);
    }
}
