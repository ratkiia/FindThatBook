# Find That Book — Architecture & Design

## A guide for developers, reviewers, and presentations

---

## 1. What the application does (in plain language)

Readers rarely remember a book precisely. They type things like *"tolkien hobbit illustrated
deluxe 1937"*, *"twilight meyer"*, or *"mark huckleberry"*. **Find That Book** takes that messy
text and returns an ordered list of the most likely real books, each with a one-line reason
for why it matched — for example *"Exact title match; Tolkien is the primary author; the 1937
year matches."*

Under the hood it combines three ideas:

1. **Artificial Intelligence (Google Gemini)** to *understand* the messy text — figuring out
   which words are a title, which are an author, and what the leftover hints mean.
2. **A public book database (Open Library)** to *find* real candidate books.
3. **A deterministic ranking engine** to *decide*, transparently and repeatably, which
   candidates are the best matches.

The key product principle: **AI makes it smart; deterministic logic makes it reliable.** If
the AI is unavailable, the app still works.

---

## 2. The big picture

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

---

## 3. Why Clean Architecture?

The backend is organized into four concentric layers. Dependencies always point **inward** —
inner layers never know about outer ones.

```
        ┌────────────────────────────────────────────┐
        │  Api  (HTTP, Swagger, DI, error handling)    │   ← outermost, replaceable
        │   ┌──────────────────────────────────────┐  │
        │   │  Infrastructure (Gemini, OpenLibrary, │  │   ← talks to the outside world
        │   │  caching, resilient HTTP)             │  │
        │   │   ┌────────────────────────────────┐  │  │
        │   │   │  Application (use cases:        │  │  │   ← orchestration + ranking
        │   │   │  orchestrator, ranking engine)  │  │  │
        │   │   │   ┌─────────────────────────┐   │  │  │
        │   │   │   │  Domain (entities, rules,│   │  │  │   ← pure business rules,
        │   │   │   │  text normalization)     │   │  │  │      zero dependencies
        │   │   │   └─────────────────────────┘   │  │  │
        │   │   └────────────────────────────────┘  │  │
        │   └──────────────────────────────────────┘  │
        └────────────────────────────────────────────┘
```

- **Domain** — the vocabulary of the problem: a `CanonicalWork`, a `BookAuthor` (primary vs.
  contributor), the `MatchTier` hierarchy, and pure text rules (normalization, similarity). No
  frameworks, no HTTP, no database. It is trivially unit-testable.
- **Application** — the *use cases*. The `BookSearchService` orchestrates the pipeline; the
  `RankingEngine` scores candidates. This layer defines **interfaces** (e.g. "something that can
  extract fields", "something that can search Open Library") without caring how they're
  implemented.
- **Infrastructure** — the *implementations* of those interfaces: the Gemini client, the Open
  Library client, caching, and resilient HTTP. If we swapped Gemini for another model, or
  Open Library for another catalogue, only this layer changes.
- **Api** — the thin outer shell: HTTP endpoints, validation, Swagger, health checks, tracing,
  and wiring everything together with dependency injection.

### Benefits

- **Testability.** The most important logic (ranking, normalization) is pure and has no
  external dependencies, so it is fast and reliable to test. External services are hidden
  behind interfaces and easily faked.
- **Replaceability.** Gemini, Open Library, and the cache are all behind interfaces. Swapping
  any of them is a localized change.
- **Clarity.** A new developer can read the `Application` layer and understand *what the
  system does* without wading through HTTP or JSON details.
- **Longevity.** Business rules don't rot when frameworks change, because they don't depend on
  frameworks.

### Trade-offs (the honest cons)

- **More moving parts.** Four projects and a set of interfaces are more ceremony than a single
  file. For a tiny script this would be overkill; for an evaluated, extensible service it pays
  off quickly.
- **Indirection.** Following a request means hopping through an interface. The upside is that
  each hop is independently testable and replaceable.

---

## 4. The processing pipeline, step by step

1. **Normalize** the raw text (lowercase, strip accents and punctuation, collapse spaces).
2. **Extract fields with Gemini** → `{ title?, author?, keywords[] }`. Gemini resolves hints
   like *"mark huckleberry" → author "Mark Twain"*. *If Gemini fails, a deterministic
   extractor keeps the words as keywords.*
3. **Retrieve candidates** from Open Library using up to four strategies **in parallel** —
   title+author, title, author, and a free-text catch-all — then merge and **de-duplicate by
   canonical work** so *The Hobbit* and *The Hobbit: There and Back Again* collapse into one.
4. **Rank** every candidate with the deterministic engine into a tier + score.
5. **Enrich** the top few with extra detail (description, subjects) to ground explanations.
6. **Explain** each top result with Gemini — *fed the ranking signals and told not to invent
   facts.* If Gemini fails, a rule-based explanation is used instead.

### The matching hierarchy (how "best" is decided)

| Tier | Meaning | Example |
|------|---------|---------|
| 1 | Exact title **+ primary author** | "hobbit tolkien" → *The Hobbit* by Tolkien |
| 2 | Exact title **+ contributor** author | title matches, but the person is the illustrator |
| 3 | Near (fuzzy) title + primary author | "the hobit tolkien" (typo) |
| 4 | Author only | "dickens" → top Dickens works |
| 5 | Keyword only | "huckleberry" → *Huckleberry Finn* |

Within a tier, a continuous 0–1 score orders results using title similarity, partial-author
matching, publication-year match, keyword overlap, and a popularity tiebreak.

---

## 5. Salient features (talking points for a presentation)

- **Real AI, used well.** Gemini does what LLMs are good at — interpreting fuzzy human intent
  and writing natural language — while deterministic code does what it's good at: precise,
  repeatable ranking. The explanations are *grounded* in fetched facts to curb hallucination.
- **It never falls over.** No API key? Gemini timed out? Malformed AI response? Open Library
  hiccup? The service still returns sensible, ranked results and tells the caller what
  happened via `notes` and `aiExtractionUsed`/`aiExplanationsUsed` flags.
- **Transparent, not a black box.** Every result carries its tier, score, and the concrete
  signals that produced it. Great for trust and for debugging.
- **Production-minded.** Resilient HTTP (automatic retries, circuit breaker, timeouts),
  in-memory caching to respect Open Library's rate limits, health checks, OpenTelemetry
  tracing, structured logging, Swagger docs, and RFC-7807 error responses.
- **Handles the tricky data.** Subtitle variants, partial author names, and Open Library's
  habit of listing illustrators/editors alongside the real author are all accounted for.

---

## 6. Reliability & resilience — how failure is handled

| Failure | What happens |
|---------|--------------|
| No Gemini API key | AI services aren't registered; deterministic extraction + rule-based explanations are used. |
| Gemini timeout / 5xx | Resilience layer retries; if still failing, the orchestrator catches it and falls back. |
| Gemini returns malformed JSON | The parser tolerates code fences/prose; on genuine failure it falls back. |
| Open Library down / rate-limited | Retries + circuit breaker; a failing strategy returns empty without sinking the others. |
| No candidates found | Returns an empty list with an explanatory note (never a wrong guess). |
| Unexpected server error | Central handler returns a clean ProblemDetails 500; internals never leak. |

---

## 7. Performance & scaling

- **Parallelism.** The four search strategies and the enrichment calls run concurrently, so
  latency is roughly the slowest single call, not the sum.
- **Caching.** Identical searches and work look-ups are served from an in-memory cache
  (configurable TTL, default 30 minutes), cutting latency and outbound traffic.
- **Stateless API.** No server-side session or database, so it scales horizontally behind a
  load balancer. The only shared state is the cache, which can be moved to Redis by swapping
  one interface — no business-logic changes.

---

## 8. Technology choices and why

| Choice | Why | Alternatives considered |
|--------|-----|-------------------------|
| .NET 8 Web API | Required; excellent async I/O, DI, and resilience tooling | — |
| Clean Architecture | Testability, replaceability, clarity | Single-project MVC (simpler but less extensible) |
| Google Gemini | Required default; strong free tier | OpenAI/others (documented as swappable) |
| Deterministic ranking | Reproducible, testable, works without AI | LLM-only ranking (opaque, non-deterministic) |
| In-memory cache | Zero-infra, fits a stateless service | Redis (future, for multi-instance) |
| React + Vite + Tailwind | Fast, modern, responsive UI with minimal boilerplate | Blazor, plain JS |
| xUnit + Moq + FluentAssertions | Readable, industry-standard .NET testing | NUnit/MSTest |

---

## 9. Glossary (for non-technical readers)

- **API** — the backend service the UI talks to; it has no screen of its own.
- **LLM / Gemini** — a large language model; software that understands and generates natural
  language.
- **Open Library** — a free, public online catalogue of books.
- **Canonical work** — the single "real" book behind its many editions and title variations.
- **Ranking / tier / score** — the rules that order results from best to worst match.
- **Resilience** — automatically coping with slow or failing external services.
- **Cache** — a short-term memory of recent answers to avoid repeating work.

---

## 10. Summary

Find That Book is a compact but production-shaped system: a clean, layered .NET 8 backend that
uses Gemini for the *fuzzy human* parts (understanding intent, explaining results) and
deterministic, well-tested logic for the *precise machine* parts (finding and ranking books).
It is transparent about its reasoning, resilient to every external failure it depends on, and
structured so that any part — the AI model, the book source, the cache, the UI — can be
replaced without disturbing the core.
