/** Maps the backend MatchTier enum name to a friendly label and Tailwind color classes. */
export interface TierStyle {
  label: string;
  classes: string;
  rank: number;
}

const TIERS: Record<string, TierStyle> = {
  ExactTitlePrimaryAuthor: { label: 'Exact title + primary author', classes: 'bg-emerald-100 text-emerald-800', rank: 1 },
  ExactTitleContributorAuthor: { label: 'Exact title · contributor author', classes: 'bg-teal-100 text-teal-800', rank: 2 },
  NearTitlePrimaryAuthor: { label: 'Near title match', classes: 'bg-sky-100 text-sky-800', rank: 3 },
  AuthorOnly: { label: 'Author match', classes: 'bg-indigo-100 text-indigo-800', rank: 4 },
  KeywordOnly: { label: 'Keyword match', classes: 'bg-amber-100 text-amber-800', rank: 5 },
};

export function tierStyle(tier: string): TierStyle {
  return TIERS[tier] ?? { label: tier, classes: 'bg-slate-100 text-slate-700', rank: 99 };
}
