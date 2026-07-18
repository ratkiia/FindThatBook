namespace FindThatBook.Application.Abstractions;

/// <summary>
/// Extra fields fetched from /works/{id}.json to ground explanations. 
/// </summary>
/// <param name="Description">Free-text work description, if any.</param>
/// <param name="Subjects">Subject tags (genres, characters, places).</param>
public sealed record WorkDetails(string? Description, IReadOnlyList<string> Subjects);
