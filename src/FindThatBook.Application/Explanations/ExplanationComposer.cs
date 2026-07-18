using System.Text;
using FindThatBook.Domain.Books;

namespace FindThatBook.Application.Explanations;

/// <summary>
/// Builds a grounded "why this book" explanation purely from the deterministic ranking signals.
/// </summary>
public sealed class ExplanationComposer
{
    public string Compose(ExtractedFields query, ScoredWork scored)
    {
        var work = scored.Work;
        var author = work.PrimaryAuthor?.Name ?? "an unknown author";

        var sb = new StringBuilder();
        sb.Append($"\"{work.FullTitle}\" by {author}");
        if (work.FirstPublishYear is { } year)
        {
            sb.Append($" (first published {year})");
        }

        if (scored.Signals.Count > 0)
        {
            sb.Append(" — ");
            sb.Append(string.Join("; ", scored.Signals));
        }
        else
        {
            sb.Append(" — closest available match for the query");
        }

        sb.Append('.');
        return sb.ToString();
    }
}
