import { useRef, useState } from 'react';
import { searchBooks, type BookSearchResult } from './api';
import { BookCard } from './components/BookCard';

const EXAMPLES = [
  'dickens',
  'tale two cities',
  'twilight meyer',
  'tolkien hobbit illustrated deluxe 1937',
  'mark huckleberry',
  'austen bennet',
];

export default function App() {
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<BookSearchResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  async function runSearch(q: string) {
    const trimmed = q.trim();
    if (!trimmed) return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setLoading(true);
    setError(null);
    try {
      const data = await searchBooks(trimmed, controller.signal);
      setResult(data);
    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        setError((err as Error).message);
        setResult(null);
      }
    } finally {
      setLoading(false);
    }
  }

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    void runSearch(query);
  }

  function useExample(example: string) {
    setQuery(example);
    void runSearch(example);
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900">
      <div className="mx-auto max-w-3xl px-4 py-10">
        <header className="mb-6">
          <h1 className="text-3xl font-bold tracking-tight">Find That Book</h1>
          <p className="mt-1 text-slate-600">
            Type a messy, half-remembered query — an author, a title, a character, or all of it — and
            we&apos;ll find the most likely book.
          </p>
        </header>

        <form onSubmit={onSubmit} className="flex gap-2">
          <input
            type="text"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="e.g. tolkien hobbit illustrated deluxe 1937"
            aria-label="Book search query"
            className="flex-1 rounded-lg border border-slate-300 bg-white px-4 py-2.5 shadow-sm outline-none focus:border-sky-500 focus:ring-2 focus:ring-sky-200"
          />
          <button
            type="submit"
            disabled={loading || !query.trim()}
            className="rounded-lg bg-sky-600 px-5 py-2.5 font-medium text-white shadow-sm transition hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {loading ? 'Searching…' : 'Search'}
          </button>
        </form>

        <div className="mt-3 flex flex-wrap gap-2">
          <span className="text-xs text-slate-400">Try:</span>
          {EXAMPLES.map((example) => (
            <button
              key={example}
              onClick={() => useExample(example)}
              className="rounded-full border border-slate-200 bg-white px-2.5 py-0.5 text-xs text-slate-600 transition hover:border-sky-300 hover:text-sky-700"
            >
              {example}
            </button>
          ))}
        </div>

        {error && (
          <div className="mt-6 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700" role="alert">
            {error}
          </div>
        )}

        {loading && !result && (
          <div className="mt-6 space-y-3">
            {[0, 1, 2].map((i) => (
              <div key={i} className="h-40 animate-pulse rounded-xl bg-slate-100" />
            ))}
          </div>
        )}

        {result && <Results result={result} loading={loading} />}
      </div>
    </div>
  );
}

function Results({ result, loading }: { result: BookSearchResult; loading: boolean }) {
  const { interpreted } = result;
  const hasInterpretation = interpreted.title || interpreted.author || interpreted.keywords.length > 0;

  return (
    <section className={`mt-6 ${loading ? 'opacity-60' : ''}`}>
      <div className="mb-4 rounded-lg border border-slate-200 bg-white p-4">
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="font-medium text-slate-700">Interpreted as:</span>
          {interpreted.title && <Chip label={`title: ${interpreted.title}`} />}
          {interpreted.author && <Chip label={`author: ${interpreted.author}`} />}
          {interpreted.keywords.map((k) => (
            <Chip key={k} label={k} muted />
          ))}
          {!hasInterpretation && <span className="text-slate-400">(no fields extracted)</span>}
        </div>

        <div className="mt-2 flex flex-wrap gap-2 text-xs">
          <Badge on={result.aiExtractionUsed} label="AI extraction" />
          <Badge on={result.aiExplanationsUsed} label="AI explanations" />
        </div>

        {result.notes.length > 0 && (
          <ul className="mt-2 space-y-0.5 text-xs text-slate-500">
            {result.notes.map((note) => (
              <li key={note}>• {note}</li>
            ))}
          </ul>
        )}
      </div>

      {result.matches.length === 0 ? (
        <p className="rounded-lg border border-slate-200 bg-white p-6 text-center text-slate-500">
          No confident matches for “{result.query}”. Try adding an author or a distinctive title word.
        </p>
      ) : (
        <div className="space-y-3">
          {result.matches.map((match, i) => (
            <BookCard key={match.workId} match={match} rank={i + 1} />
          ))}
        </div>
      )}
    </section>
  );
}

function Chip({ label, muted }: { label: string; muted?: boolean }) {
  return (
    <span className={`rounded px-2 py-0.5 ${muted ? 'bg-slate-100 text-slate-600' : 'bg-sky-100 text-sky-800'}`}>
      {label}
    </span>
  );
}

function Badge({ on, label }: { on: boolean; label: string }) {
  return (
    <span
      className={`rounded-full px-2 py-0.5 font-medium ${
        on ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-500'
      }`}
    >
      {on ? '✓' : '○'} {label}
    </span>
  );
}
