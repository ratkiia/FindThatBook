export interface BookMatch {
  title: string;
  subtitle?: string;
  authors: string[];
  primaryAuthor?: string;
  firstPublishYear?: number;
  coverUrl?: string;
  openLibraryUrl: string;
  workId: string;
  explanation: string;
  matchTier: string;
  score: number;
  signals: string[];
}

export interface InterpretedQuery {
  title?: string;
  author?: string;
  keywords: string[];
}

export interface BookSearchResult {
  query: string;
  interpreted: InterpretedQuery;
  matches: BookMatch[];
  aiExtractionUsed: boolean;
  aiExplanationsUsed: boolean;
  notes: string[];
}

/** POSTs the query to the .NET API. Throws with a useful message on failure. */
export async function searchBooks(query: string, signal?: AbortSignal): Promise<BookSearchResult> {
  const response = await fetch('/api/books/search', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ query }),
    signal,
  });

  if (!response.ok) {
    let detail = `Request failed (${response.status}).`;
    try {
      const problem = await response.json();
      detail = problem.detail ?? problem.title ?? detail;
      if (problem.errors) {
        detail = Object.values(problem.errors).flat().join(' ');
      }
    } catch {
      /* response was not JSON — keep the default message */
    }
    throw new Error(detail);
  }

  return response.json();
}
