using System.ComponentModel.DataAnnotations;

namespace FindThatBook.Api.Models;

/// <summary>Request body for POST /api/books/search.</summary>
public sealed class SearchRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "A non-empty 'query' is required.")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Query must be between 1 and 300 characters.")]
    public string Query { get; set; } = string.Empty;
}
