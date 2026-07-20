
# FindThatBook
=======
# Find That Book

A .NET 8 + Gemini-powered library discovery service. Give it a messy, half-remembered
query — an author, a title, a character name, or a noisy mix of all three — and it returns
an ordered list of the most likely [Open Library](https://openlibrary.org) books, each with
a short, grounded "why this book" explanation.

| Query | Top result |
|-------|-----------|
| `tolkien hobbit illustrated deluxe 1937` | *The Hobbit* — J.R.R. Tolkien (1937) |
| `twilight meyer` | *Twilight* — Stephenie Meyer |
| `mark huckleberry` | *Adventures of Huckleberry Finn* — Mark Twain |
| `dickens` | Top works by Charles Dickens |

---

## Contents
- [Architecture at a glance](#architecture-at-a-glance)
- [Prerequisites](#prerequisites)
- [Setup & running](#setup--running)
  - [1. Configure the Gemini API key](#1-configure-the-gemini-api-key)
  - [2. Run the backend](#2-run-the-backend)
  - [3. Run the frontend](#3-run-the-frontend)
- [How it works (implementation overview)](#how-it-works-implementation-overview)
- [Features implemented](#features-implemented)
- [Testing strategy](#testing-strategy)
- [Assumptions & design decisions](#assumptions--design-decisions)
- [Future improvements](#future-improvements)
- [Requirement cross-check](#requirement-cross-check)

---

## Architecture at a glance
 The big picture:

```
        ┌──────────────┐        ┌───────────────────────────┐        ┌──────────────────┐
        │  React UI     │  POST  │      .NET 8 Web API        │        │  Google Gemini    │
        │ (search box,  │ ─────▶ │  (orchestration + ranking) │ ─────▶ │ (extract fields,  │
        │  results)     │ ◀───── │                            │ ◀───── │  write reasons)   │
        └──────────────┘  JSON   └───────────────────────────┘        └──────────────────┘
                                          │        ▲
                                          ▼        │
                                 ┌───────────────────────────┐
                                 │      Open Library APIs      │
                                 │  search / works / authors   │
                                 └───────────────────────────┘
```

The user talks only to the React UI. The UI talks only to our .NET API. The API is the brain:
it calls Gemini to interpret the query, calls Open Library to fetch candidate books, ranks
them, and (optionally) asks Gemini to explain each result.

Clean Architecture with strict inward-pointing dependencies
(`Api → Application → Domain`, `Infrastructure → Application → Domain`):

```
src/
├── FindThatBook.Domain          # Entities, value objects, pure text rules (no dependencies)
├── FindThatBook.Application      # Orchestration, ranking engine, interfaces (the use cases)
├── FindThatBook.Infrastructure   # Gemini + Open Library adapters, caching, resilient HTTP
└── FindThatBook.Api              # ASP.NET Core Web API, DI, Swagger, health, tracing
tests/
└── FindThatBook.Tests            # Unit + integration tests (xUnit)
frontend/                         # React + Vite + TypeScript + Tailwind UI
```

The processing pipeline:

```
query ─▶ normalize ─▶ AI field extraction ─▶ multi-strategy Open Library retrieval
      ─▶ canonical de-dup ─▶ deterministic ranking ─▶ enrich top matches
      ─▶ AI explanations ─▶ ordered results
```

Every AI step **degrades gracefully**: if Gemini is missing, slow, or returns malformed
JSON, the pipeline still produces deterministically-ranked results with rule-based
explanations. See `Architecture.md` / `Architecture.docx` for the full narrative.

---

## Prerequisites

- **.NET SDK 8.0+** (the projects target `net8.0`).
  > This repo was developed on a machine that only had the **.NET 10** runtime, so the
  > projects set `<RollForward>Major</RollForward>` (in `Directory.Build.props`) which lets
  > the `net8.0` assemblies run on a newer shared framework. On a normal .NET 8 machine this
  > is a no-op. The **test project** targets `net10.0` for a runtime-specific reason noted in
  > [Assumptions](#assumptions--design-decisions); it still references the `net8.0` app.
- **Node.js 18+** and npm (for the frontend).
- A **Google Gemini API key** — free tier available at
  <https://ai.google.dev/gemini-api/docs/api-key>. *Optional:* the app runs without it using
  the deterministic fallback path.

---

## Setup & running

### 1. Configure the Gemini API key

The key is read from configuration key `Gemini:ApiKey`. Use whichever is convenient — **do
not commit your key**:

**Option A — user secrets (recommended for local dev):**
```bash
cd src/FindThatBook.Api
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY"
```

**Option B — environment variable:**
```bash
# PowerShell
$env:Gemini__ApiKey = "YOUR_GEMINI_API_KEY"
# bash
export Gemini__ApiKey="YOUR_GEMINI_API_KEY"
```

(Note the double underscore `__` maps to the `:` config separator.)

**Option C — appsettings:** set `Gemini:ApiKey` in `src/FindThatBook.Api/appsettings.Development.json`
(git-ignored variants like `appsettings.*.local.json` are safest).

Model and other settings are configurable under the `Gemini` section (default model:
`gemini-2.0-flash`). If you swap models, just change `Gemini:Model`.

### 2. Run the backend

```bash
dotnet restore
dotnet run --project src/FindThatBook.Api
```

- API base: `http://localhost:5099`
- Swagger UI: `http://localhost:5099/swagger`
- Health: `http://localhost:5099/health`

Try it:
```bash
curl -X POST http://localhost:5099/api/books/search \
  -H "Content-Type: application/json" \
  -d '{ "query": "tolkien hobbit illustrated deluxe 1937" }'
```

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The Vite dev server proxies `/api/*` to the backend on
`http://localhost:5099` (override with the `VITE_API_TARGET` env var), so there are no CORS
issues in development. The API also has a CORS policy allowing `localhost:5173`.

### Run the tests

```bash
dotnet test
```

---

## How it works (implementation overview)

1. **Normalization** (`Domain/Text/TextNormalizer`): lowercase, strip diacritics
   (`Émile → emile`), remove punctuation, collapse whitespace, drop stop-words.
2. **Field extraction** (`Infrastructure/Gemini/GeminiFieldExtractor`): Gemini turns the blob
   into `{ title?, author?, keywords[] }`, resolving hints like *"mark huckleberry" → author
   "Mark Twain"*. If Gemini is unavailable, `FallbackFieldExtractor` keeps the tokens as
   keywords and lets Open Library's free-text search do the work.
3. **Candidate retrieval** (`Application/Search/CandidateRetriever`): runs up to four Open
   Library strategies **concurrently** — (A) title+author, (B) title, (C) author, (D)
   free-text `q=` — and merges them, de-duplicating by canonical **work key**.
4. **Ranking** (`Application/Ranking/RankingEngine`): a deterministic engine assigns each
   candidate a **tier** (the matching hierarchy) plus a continuous 0–1 score for intra-tier
   ordering:
   1. Exact title + **primary** author
   2. Exact title + **contributor** author
   3. Near (fuzzy) title + primary author
   4. Author-only
   5. Keyword-only

   Scoring blends title similarity (token-set Jaccard + Levenshtein, subtitle-tolerant),
   partial-author matching (`tolkien → J.R.R. Tolkien`), publication-year match, keyword
   overlap, and an edition-count popularity tiebreak.
5. **Enrichment**: the top matches are enriched via `/works/{id}.json` (description, subjects)
   to ground explanations, then re-ranked.
6. **Explanations** (`Infrastructure/Gemini/GeminiExplanationGenerator`): Gemini writes a
   one-sentence, grounded explanation per candidate, **given the deterministic signals** so it
   stays factual. Falls back to a rule-based `ExplanationComposer`.

The response also surfaces `aiExtractionUsed`, `aiExplanationsUsed`, the interpreted fields,
and human-readable `notes` — so the pipeline is transparent, not a black box.

### API

`POST /api/books/search`
```json
{ "query": "tolkien hobbit illustrated deluxe 1937" }
```
returns
```json
{
  "query": "...",
  "interpreted": { "title": "The Hobbit", "author": "Tolkien", "keywords": ["illustrated", "deluxe", "1937"] },
  "aiExtractionUsed": true,
  "aiExplanationsUsed": true,
  "notes": [],
  "matches": [
    {
      "title": "The Hobbit",
      "primaryAuthor": "J.R.R. Tolkien",
      "firstPublishYear": 1937,
      "coverUrl": "https://covers.openlibrary.org/b/id/....jpg",
      "openLibraryUrl": "https://openlibrary.org/works/OL27482W",
      "matchTier": "ExactTitlePrimaryAuthor",
      "score": 0.95,
      "signals": ["Exact/normalized title match", "J.R.R. Tolkien is the primary author", "First published 1937 matches the query"],
      "explanation": "Exact title match; Tolkien is the primary author; the 1937 first-publication year matches the query."
    }
  ]
}
```

---

## Features implemented

- ✅ `POST /api/books/search` accepting messy free-text (sparse, dense/noisy, ambiguous).
- ✅ **Gemini AI** for field extraction **and** grounded per-candidate explanations.
- ✅ Multi-strategy Open Library retrieval + canonical (work-key) de-duplication.
- ✅ Subtitle/alternate-title tolerance (*The Hobbit* ↔ *The Hobbit: There and Back Again*).
- ✅ Primary-author vs. contributor distinction reflected in ranking and explanations.
- ✅ Full **matching hierarchy** with transparent tiers, scores and signals.
- ✅ **Graceful degradation** — works with no API key, on Gemini timeout, or on malformed JSON.
- ✅ Resilient HTTP (retries, circuit breaker, timeouts) + in-memory caching with TTL.
- ✅ Swagger/OpenAPI, health checks, OpenTelemetry tracing, structured logging, ProblemDetails errors.
- ✅ Responsive React + Tailwind UI: search box, example chips, covers, explanations,
  tier badges, score bars, loading skeletons, and error states.

---

## Testing strategy

`dotnet test` — 37 tests, all green. The strategy targets the highest-value, most
deterministic logic and the graceful-degradation guarantees:

- **Domain unit tests** — normalization (diacritics, punctuation, stop-words) and similarity
  (Jaccard, Levenshtein, subtitle containment). These are pure and fast.
- **Ranking unit tests** — one test per tier of the matching hierarchy, plus subtitle
  tolerance, partial-author matching (`meyer → Stephenie Meyer`), ordering (tier then score),
  and exclusion of unrelated works. This is the core IP, so it gets the most coverage.
- **Orchestrator tests** (`BookSearchService`) — the end-to-end `twilight meyer → Twilight`
  example with a fake Open Library client and a mocked Gemini; **fallback-on-AI-failure**;
  rule-based explanations when no AI is configured; and verification that all four search
  strategies fire.
- **Infrastructure tests** — the LLM JSON parser tolerates Markdown code fences and prose
  around the JSON (a real-world LLM failure mode).
- **Integration tests** (`WebApplicationFactory`) — boot the real API with an in-memory Open
  Library fake: `200` + ranked matches, `400` on empty query (validation), and `/health`.

External services (Gemini, Open Library) are faked/mocked so the suite is fast and
network-independent.

---

## Assumptions & design decisions

- **Framework version / RollForward.** The brief specifies .NET 8, so the app targets
  `net8.0`. The dev machine only had the .NET 10 runtime, hence `RollForward=Major`. On a
  real .NET 8 machine this changes nothing.
- **Test project targets `net10.0`.** The in-process ASP.NET Core `TestServer` running
  `net8.0` assemblies on the `net10` shared framework hits a `System.Text.Json` /
  `PipeWriter.UnflushedBytes` incompatibility. Targeting the *test* project at `net10.0`
  (it still references the `net8.0` app) sidesteps this; the shipped code stays on .NET 8.
  On a machine with the .NET 8 runtime installed you can set it back to `net8.0`.
- **Primary vs. contributor authors.** Open Library's `search.json` returns work-level
  authors (creators) and doesn't cleanly flag edition contributors. I apply a documented
  heuristic: the **first** listed author is `Primary`, the rest are `Contributor`, so the
  ranking engine can down-weight contributor-only matches — matching the brief's data-quality
  note. A fuller solution would resolve roles from `/works/{id}.json` authors + edition data.
- **AI is assistive, ranking is deterministic.** Gemini extracts fields and writes
  explanations; the *ranking* is deterministic and testable. This keeps results reproducible
  and the system usable even when the LLM is down. Gemini re-ranking is left as an opt-in
  future step.
- **Grounded explanations.** The explanation prompt is fed the concrete ranking signals and
  told not to invent facts, reducing hallucination.
- **Stateless + in-memory cache.** No database; search/work responses are cached in memory
  (default 30 min TTL). Swapping in Redis is a one-interface change (`IMemoryCache` →
  distributed cache).
- **Confidence.** A 0–1 score is returned for transparency, but tiers are the primary signal
  (the brief says numeric confidence isn't required).

---

## Future improvements

- **Optional Gemini re-ranking** of the top 5–10 candidates as a final, clearly-labelled pass.
- **Author-role resolution** from `/works/{id}.json` + editions for a precise primary/contributor
  split instead of the position heuristic.
- **Redis** distributed cache + response caching headers for horizontal scaling.
- **Spelling correction / transliteration** before extraction for very noisy input.
- **Better cross-edition canonicalization** (merge near-duplicate works, not just identical keys).
- **Frontend polish**: virtualized lists, result pagination, keyboard nav, and unit tests
  (Vitest + Testing Library).
- **Containerization** (Dockerfiles + compose) and a live demo deployment.
- **log user queries**:Log user queries for future model training, platform enhancements, analytics, auditing, troubleshooting, and end-to-end request tracing.
- **Introduce Guardrails**: Enforces relevance, safety, and moderation checks to prevent prompt injection, jailbreaks, harmful content, and other unauthorized interactions before LLM processing.
- **Agentic AI**: Leverage Agentic AI to orchestrate multi-step book discovery workflows, including query understanding, source selection, metadata enrichment, relevance ranking, and personalized recommendations.
- **User Authentication and Autherization**:OKTA or another third-party Identity and Access Management (IAM) solution can be integrated to provide secure authentication and authorization for the application..
- **OTLP exporter** wired to a collector (Jaeger/Tempo) for distributed tracing in non-dev envs.
  (The OTLP exporter package was removed to clear a dependency CVE; one moderate transitive
  advisory in `OpenTelemetry.Api` currently has no upstream fix and is left un-suppressed.)
---

## Minor notes
- OpenTelemetry tracing is wired with a **console exporter** in Development; the OTLP exporter
  package was removed to clear a dependency CVE and is listed as a future add-back.