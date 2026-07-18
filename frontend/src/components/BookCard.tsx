import type { BookMatch } from '../api';
import { tierStyle } from '../tier';

export function BookCard({ match, rank }: { match: BookMatch; rank: number }) {
  const tier = tierStyle(match.matchTier);
  const scorePct = Math.round(match.score * 100);

  return (
    <article className="flex gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition hover:shadow-md">
      <div className="flex-shrink-0">
        {match.coverUrl ? (
          <img
            src={match.coverUrl}
            alt={`Cover of ${match.title}`}
            className="h-36 w-24 rounded-md object-cover ring-1 ring-slate-200"
            loading="lazy"
          />
        ) : (
          <div className="flex h-36 w-24 items-center justify-center rounded-md bg-slate-100 text-center text-xs text-slate-400 ring-1 ring-slate-200">
            No cover
          </div>
        )}
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex items-start justify-between gap-2">
          <h3 className="text-lg font-semibold text-slate-900">
            <span className="mr-1 text-slate-400">#{rank}</span>
            {match.title}
            {match.subtitle ? <span className="font-normal text-slate-500">: {match.subtitle}</span> : null}
          </h3>
          <span className={`whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${tier.classes}`}>
            {tier.label}
          </span>
        </div>

        <p className="mt-0.5 text-sm text-slate-600">
          {match.primaryAuthor ?? match.authors.join(', ') ?? 'Unknown author'}
          {match.firstPublishYear ? ` · ${match.firstPublishYear}` : ''}
        </p>

        <p className="mt-2 text-sm leading-relaxed text-slate-700">{match.explanation}</p>

        {match.signals.length > 0 && (
          <ul className="mt-2 flex flex-wrap gap-1.5">
            {match.signals.map((signal) => (
              <li key={signal} className="rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">
                {signal}
              </li>
            ))}
          </ul>
        )}

        <div className="mt-3 flex items-center gap-3">
          <div className="flex items-center gap-2" title={`Relevance score ${scorePct}%`}>
            <div className="h-1.5 w-28 overflow-hidden rounded-full bg-slate-100">
              <div className="h-full rounded-full bg-slate-400" style={{ width: `${scorePct}%` }} />
            </div>
            <span className="text-xs text-slate-400">{scorePct}%</span>
          </div>
          <a
            href={match.openLibraryUrl}
            target="_blank"
            rel="noreferrer"
            className="text-sm font-medium text-sky-600 hover:text-sky-800 hover:underline"
          >
            View on Open Library ↗
          </a>
        </div>
      </div>
    </article>
  );
}
