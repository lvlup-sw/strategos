# Plan — Ontology 2.5.0 Docs + Starlight Migration

**Design:** `docs/designs/2026-05-15-ontology-2-5-0-docs-starlight.md`
**Workflow:** feature
**Iron Law:** No production code without a failing test first.

## TDD discipline adapted for docs work

Docs migration adapts RED → GREEN → REFACTOR as:

| Phase | Code-side | Docs-side |
|---|---|---|
| **RED** | Failing test (xUnit / vitest) | Failing acceptance gate: URL doesn't resolve, build warns, Pagefind misses, or prose contains a banned AI-register term |
| **GREEN** | Implement minimum code | Write the markdown / config / workflow YAML |
| **REFACTOR** | Tighten code | Tighten prose against DIM-8 invariants and normalize headings |

Test gates are real CLI commands that run against the build output:

- `BUILD_OK` — `cd docs && npm run build` exits 0 with zero `[WARN]` lines
- `URL_RESOLVES <slug>` — `test -f docs/dist/<slug>/index.html`
- `TERM_INDEXED <term>` — Pagefind index in `docs/dist/pagefind/` contains the term (greppable)
- `PROSE_CLEAN <file>` — `grep -iE '(delve|tapestry|robust|crucial|testament|landscape)' <file>` returns no matches

## Spec traceability

Each design section maps to one or more tasks:

| Design § | Tasks |
|---|---|
| §3 Non-goals — defer #32 | T01 |
| §4 Migration shape — file layout | T05–T06 |
| §5 Astro/Starlight config | T03, T04 |
| §5 Trailing-slash decision | T08 |
| §6 Sidebar design | T04 (config), T22 (verification) |
| §7 Guide tier (8 pages) | T09–T16 |
| §7 Reference tier (8 pages) | T17–T20 |
| §7 Diagnostics tier (4 pages) | T21 |
| §8 GitHub Pages workflow change | T22 |
| §9 Implementation checklist | T23 (acceptance) |
| §11 Acceptance criteria | T23 (acceptance) |

## Streams

```
Stream A (foundation):     T01 → T02 → T03 → T04 → T05 (sequential, blocks all others)
Stream B (content move):   T06 → T07 → T08                (depends on A)
Stream C (Ontology guide): T09..T16                       (parallel after B; per-file)
Stream D (Ontology ref):   T17..T20                       (parallel after B; per-file)
Stream E (Diagnostics):    T21                            (depends on B)
Stream F (workflow):       T22                            (depends on B)
Stream G (acceptance):     T23                            (depends on all above)
```

Streams C and D can run as 12 parallel worktrees because each task touches a distinct content file.

---

## Stream A — Foundation

### T01: Defer #32 from 2.5.0 milestone

**Phase:** GREEN (administrative; no test)
**Files:** none in repo

1. **GREEN:**
   - `gh issue edit 32 --milestone "Ontology 2.6.0 — Hybrid Retrieval Seams"`
   - `gh issue comment 32 --body "Deferred from 2.5.0 per design doc 2026-05-15-ontology-2-5-0-docs-starlight.md §3. The original #32 body sets 'a concrete downstream use case drives the work' as an acceptance criterion; no such consumer exists today. AONT041 MultiRegisteredTypeInLink continues to gate the case and will surface the first real driver."`

**Verification gate:** `gh issue view 32 --json milestone` returns `{"milestone": {"title": "Ontology 2.6.0 — Hybrid Retrieval Seams"}}`
**Dependencies:** none
**Parallelizable:** Yes (no file conflicts)
**testingStrategy:** none (administrative)

### T02: Add Astro + Starlight; remove VitePress

**Phase:** RED → GREEN

**Files:**
- `docs/package.json`
- `docs/package-lock.json`

1. **RED:** Add a temporary `docs/build-smoke-test.sh` that runs `cd docs && npm run build && test -d dist`. Run it. It MUST fail because Starlight isn't installed yet (or VitePress remains and the output path is `.vitepress/dist`).
2. **GREEN:**
   - Remove `vitepress` from `devDependencies`.
   - Add `astro` and `@astrojs/starlight` to `dependencies`.
   - Replace scripts: `dev`, `build`, `preview` (Astro defaults).
   - Run `npm install`.
3. **REFACTOR:** Delete `docs/build-smoke-test.sh`; the smoke step becomes part of T05.

**Verification gate:** `cd docs && npm ls @astrojs/starlight astro` exits 0; `cd docs && npm ls vitepress` reports "empty".
**Dependencies:** none
**Parallelizable:** No (modifies package.json)
**testingStrategy:** build

### T03: Write `docs/astro.config.mjs`

**Phase:** RED → GREEN → REFACTOR
**Files:** `docs/astro.config.mjs`

1. **RED:** With T02 done, attempt `cd docs && npm run build` without `astro.config.mjs`. Build fails — `site` and `base` unset; no Starlight integration.
2. **GREEN:** Write `docs/astro.config.mjs` exactly per design §5:
   - `site: 'https://lvlup-sw.github.io'`
   - `base: '/strategos/'`
   - `trailingSlash: 'always'`
   - Starlight integration with title, description, logo, social, editLink, pagefind, sidebar
3. **REFACTOR:** Compare against design §5 verbatim; flag any drift.

**Verification gate:** `cd docs && npx astro check` exits 0; `node -e 'import("./docs/astro.config.mjs")'` loads without error.
**Dependencies:** T02
**Parallelizable:** No
**testingStrategy:** build

### T04: Write `docs/src/content.config.ts`

**Phase:** RED → GREEN
**Files:** `docs/src/content.config.ts`

1. **RED:** Add stub content file `docs/src/content/docs/index.md` with frontmatter `title: Test`. Run `cd docs && npm run build`. Build fails because the `docs` collection is undeclared.
2. **GREEN:** Write `docs/src/content.config.ts` per design §5 (defineCollection with `docsLoader()` + `docsSchema()`).

**Verification gate:** `cd docs && npm run build` produces `docs/dist/index.html` from the stub.
**Dependencies:** T03
**Parallelizable:** No
**testingStrategy:** build, routing

### T05: Smoke-test the empty Starlight build

**Phase:** RED → GREEN
**Files:** none (verification only)

1. **RED:** Confirm `docs/dist/` does not exist or has no `index.html` for `/strategos/`.
2. **GREEN:** `cd docs && npm run build`. Assert:
   - `docs/dist/index.html` exists
   - `docs/dist/pagefind/` exists (Pagefind ran)
   - Zero `[WARN]` lines in the build output
3. Remove the T04 stub `docs/src/content/docs/index.md` (real index moves in T06).

**Verification gate:** `BUILD_OK` passes.
**Dependencies:** T04
**Parallelizable:** No
**testingStrategy:** build

---

## Stream B — Content migration

### T06: `git mv` content directories into Starlight layout

**Phase:** RED → GREEN
**Files:** all of `docs/learn/`, `docs/guide/`, `docs/reference/`, `docs/examples/`, `docs/index.md`

1. **RED:** Add a temporary verification script `docs/scripts/verify-slugs.sh` that asserts each of these output paths exists after build:
   - `docs/dist/index.html`
   - `docs/dist/learn/index.html`
   - `docs/dist/guide/installation/index.html`
   - `docs/dist/reference/index.html`
   - `docs/dist/examples/index.html`

   Run it. It fails — no content yet.
2. **GREEN:**
   ```bash
   git mv docs/index.md docs/src/content/docs/index.md
   git mv docs/learn    docs/src/content/docs/learn
   git mv docs/guide    docs/src/content/docs/guide
   git mv docs/reference docs/src/content/docs/reference
   git mv docs/examples docs/src/content/docs/examples
   ```
   Run `cd docs && npm run build`. Then run `docs/scripts/verify-slugs.sh`.

**Verification gate:** verify-slugs.sh exits 0; `BUILD_OK` passes (warnings expected here — see T07).
**Dependencies:** T05
**Parallelizable:** No
**testingStrategy:** build, routing

### T07: Fix dead links surfaced by Starlight

**Phase:** RED → GREEN → REFACTOR
**Files:** any markdown under `docs/src/content/docs/` whose links Starlight flags

1. **RED:** With T06 complete, run `cd docs && npm run build`. Capture every `[WARN]` line about broken links. (VitePress hid these via `ignoreDeadLinks: true`.)
2. **GREEN:** For each warned link:
   - If the target moved with T06, update the link path to the new location.
   - If the target was an excluded file (`design.md`, `deferred-features.md`, etc.), either remove the link or convert it to an external GitHub link.
   - If the target was never published, remove the link.
3. **REFACTOR:** Re-run build; expect zero `[WARN]` lines.

**Verification gate:** `BUILD_OK` passes with zero warnings.
**Dependencies:** T06
**Parallelizable:** No
**testingStrategy:** build

### T08: Verify trailing-slash behavior end-to-end

**Phase:** RED → GREEN
**Files:** none (verification only)

1. **RED:** Inspect `docs/dist/guide/installation/index.html`. Confirm internal links generated by Starlight all end in `/`.
2. **GREEN:** Start preview server (`cd docs && npm run preview`); curl `/strategos/guide/installation` (no slash) and `/strategos/guide/installation/` (with slash). Both should resolve (200 or 301→200) when deployed to GitHub Pages. Record the observed behavior in the implementation PR description; if non-200, escalate before merge.

**Verification gate:** Both URL forms resolve.
**Dependencies:** T07
**Parallelizable:** No
**testingStrategy:** routing

---

## Stream C — Ontology guide pages

All tasks in Stream C are parallel-safe (distinct files) and depend on Stream B (T08).

### T09: Guide — `index.md` (Getting Started with Ontology)

**Phase:** RED → GREEN → REFACTOR
**Files:** `docs/src/content/docs/guide/ontology/index.md`

1. **RED:** Run build; assert `docs/dist/guide/ontology/index.html` does NOT exist.
2. **GREEN:** Write the page using these sources:
   - `src/Strategos.Ontology/README.md`
   - `docs/reference/platform-architecture.md` §4.14 (now at `docs/src/content/docs/reference/platform-architecture.md` after T06)

   Content: define an object type, register with `AddOntology<T>()`, query at runtime via `IOntologyQuery`. Code samples must compile against current Strategos.Ontology API.
3. **REFACTOR:** `PROSE_CLEAN` passes. Headings normalize to sentence case (Starlight default).

**Verification gate:** `URL_RESOLVES guide/ontology/` AND `TERM_INDEXED "IOntologyQuery"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T10: Guide — `similarity-search.md`

**Files:** `docs/src/content/docs/guide/ontology/similarity-search.md`

1. **RED:** Build; `docs/dist/guide/ontology/similarity-search/index.html` does not exist.
2. **GREEN:** Sources: `src/Strategos.Ontology.Npgsql/` source + `IObjectSetProvider.ExecuteSimilarityAsync` + `ISearchable`. Cover: defining `ISearchable` properties, `IEmbeddingProvider` registration, pgvector setup, distance metric selection.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/similarity-search/` AND `TERM_INDEXED "ExecuteSimilarityAsync"` AND `TERM_INDEXED "pgvector"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T11: Guide — `text-chunking.md`

**Files:** `docs/src/content/docs/guide/ontology/text-chunking.md`

1. **RED:** Page absent from build output.
2. **GREEN:** Sources: `src/Strategos.Ontology/Chunking/` + spec §4.14.8. Cover: `ITextChunker`, `SentenceBoundaryChunker`, `ChunkOptions` (`MaxTokens`, `OverlapTokens`).
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/text-chunking/` AND `TERM_INDEXED "SentenceBoundaryChunker"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T12: Guide — `polyglot-descriptors.md`

**Files:** `docs/src/content/docs/guide/ontology/polyglot-descriptors.md`
**Implements:** #48

1. **RED:** Page absent.
2. **GREEN:** Sources: `docs/designs/2026-05-10-ontology-2-5-0-polyglot-ingestion.md`. Cover: optional `ClrType`, `SymbolKey`, `LanguageId`, AONT037 analyzer.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/polyglot-descriptors/` AND `TERM_INDEXED "SymbolKey"` AND `TERM_INDEXED "LanguageId"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T13: Guide — `ontology-sources.md`

**Files:** `docs/src/content/docs/guide/ontology/ontology-sources.md`
**Implements:** #37

1. **RED:** Page absent.
2. **GREEN:** Sources: `src/Strategos.Ontology/Sources/` + the design doc. Cover: `IOntologySource` extension contract, runtime `OntologyBuilder`, provenance metadata.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/ontology-sources/` AND `TERM_INDEXED "IOntologySource"` AND `TERM_INDEXED "provenance"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T14: Guide — `validation.md`

**Files:** `docs/src/content/docs/guide/ontology/validation.md`
**Implements:** #41, #42

1. **RED:** Page absent.
2. **GREEN:** Sources: `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`. Cover: `ontology_validate` tool, `ValidationVerdict` shape, blast-radius primitives in `IOntologyQuery`, pattern-violation surface.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/validation/` AND `TERM_INDEXED "ValidationVerdict"` AND `TERM_INDEXED "blast-radius"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T15: Guide — `read-only-dispatch.md`

**Files:** `docs/src/content/docs/guide/ontology/read-only-dispatch.md`
**Implements:** #38, #39

1. **RED:** Page absent.
2. **GREEN:** Sources: same design doc as T14, plus `src/Strategos.Ontology/` action dispatcher source. Cover: `DispatchReadOnlyAsync`, `.ReadOnly()` DSL, structured `ActionResult` constraint feedback, `IActionDispatchObserver`.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/read-only-dispatch/` AND `TERM_INDEXED "DispatchReadOnlyAsync"` AND `TERM_INDEXED "IActionDispatchObserver"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T16: Guide — `mcp-integration.md`

**Files:** `docs/src/content/docs/guide/ontology/mcp-integration.md`
**Implements:** #40

1. **RED:** Page absent.
2. **GREEN:** Sources: `docs/designs/2026-04-19-mcp-surface-conformance.md`. Cover: `OntologyToolDescriptor` MCP 2025-11-25 upgrade, `_meta` envelope, structured tool inputs/outputs.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES guide/ontology/mcp-integration/` AND `TERM_INDEXED "OntologyToolDescriptor"` AND `TERM_INDEXED "_meta"` AND `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

---

## Stream D — Ontology reference pages

### T17: Reference — overview + IOntologyQuery + IObjectSetProvider

**Files:**
- `docs/src/content/docs/reference/ontology/index.md`
- `docs/src/content/docs/reference/ontology/api/ontology-query.md`
- `docs/src/content/docs/reference/ontology/api/object-set-provider.md`

1. **RED:** None of these three pages exist in build output.
2. **GREEN:**
   - `index.md`: package map, links to guide.
   - `ontology-query.md`: surface of `IOntologyQuery` sourced from XML doc comments on the interface in `src/Strategos.Ontology/IOntologyQuery.cs` (or wherever it lives). Include `GetObjectTypeNames<T>` and 2.5.0 additions.
   - `object-set-provider.md`: `IObjectSetProvider`, `IObjectSetWriter`, `ObjectSetExpression`, `SimilarityExpression`, `FilterExpression`, `TraverseLinkExpression`.
3. **REFACTOR:** `PROSE_CLEAN` on each.

**Verification gate:** All three URLs resolve; `TERM_INDEXED "IOntologyQuery"` AND `TERM_INDEXED "TraverseLinkExpression"`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T18: Reference — IEmbeddingProvider + Dispatcher

**Files:**
- `docs/src/content/docs/reference/ontology/api/embedding-provider.md`
- `docs/src/content/docs/reference/ontology/api/dispatcher.md`

1. **RED:** Pages absent.
2. **GREEN:**
   - `embedding-provider.md`: `IEmbeddingProvider`, `EmbeddingOptions`, provider registration.
   - `dispatcher.md`: `IActionDispatcher`, `DispatchAsync`, `DispatchReadOnlyAsync`, `ActionResult`, `IActionDispatchObserver`.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** Both URLs resolve; `TERM_INDEXED "IEmbeddingProvider"` AND `TERM_INDEXED "IActionDispatcher"`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T19: Reference — Source + Graph versioning

**Files:**
- `docs/src/content/docs/reference/ontology/api/source.md`
- `docs/src/content/docs/reference/ontology/graph-versioning.md`

**Implements:** #37 (source), #44 (graph versioning)

1. **RED:** Pages absent.
2. **GREEN:**
   - `source.md`: `IOntologySource`, `OntologyBuilder` runtime surface, `ProvenanceMetadata`.
   - `graph-versioning.md`: `OntologyGraph.Version` hash — what's hashed, when it changes, when consumers should invalidate caches.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** Both URLs resolve; `TERM_INDEXED "OntologyBuilder"` AND `TERM_INDEXED "Version hash"`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

### T20: Reference — Npgsql

**Files:** `docs/src/content/docs/reference/ontology/npgsql.md`

1. **RED:** Page absent.
2. **GREEN:** Source: `src/Strategos.Ontology.Npgsql/README.md` + `PgVectorOptions.cs`. Cover: pgvector extension setup, `PgVectorOptions`, `EnsureSchemaAsync`, distance metrics, partitioning model.
3. **REFACTOR:** `PROSE_CLEAN`.

**Verification gate:** `URL_RESOLVES reference/ontology/npgsql/` AND `TERM_INDEXED "PgVectorOptions"` AND `TERM_INDEXED "EnsureSchemaAsync"`.
**Dependencies:** T08
**Parallelizable:** Yes
**testingStrategy:** build, routing, content, prose

---

## Stream E — Diagnostics

### T21: Diagnostics index — AONT codes (4 pages)

**Files:**
- `docs/src/content/docs/reference/diagnostics/index.md`
- `docs/src/content/docs/reference/diagnostics/aont-001-aont-099.md`
- `docs/src/content/docs/reference/diagnostics/aont-100-aont-199.md`
- `docs/src/content/docs/reference/diagnostics/aont-200-series.md`

**Implements:** #43 (AONT200-series)

1. **RED:** None of these URLs exist; running `grep -roh 'AONT[0-9]\{3\}' src/Strategos.Ontology* | sort -u` produces a code list with no docs counterpart.
2. **GREEN:**
   - **Code enumeration:** Run `grep -roh 'AONT[0-9]\{3\}' src/Strategos.Ontology src/Strategos.Ontology.Generators | sort -u`. For each code, locate its `DiagnosticDescriptor` to extract title, message format, severity, category.
   - `index.md`: range overview (001-099 registration; 100-199 composition + links; 200-series drift). Pagefind tip for code lookup.
   - `aont-001-aont-099.md`: table — `Code | Severity | Description | Fix | Introduced In`. Currently: AONT001, 006, 017, 023, 035, 036, 037 + 002-005, 007-016, 018-022, 024-034 (all 001-037 from generators).
   - `aont-100-aont-199.md`: AONT040, 041, 042. Include note on `AONT041`: "May be relaxed once #32 ships a concrete consumer."
   - `aont-200-series.md`: AONT200, 201, 202, 203, 204, 205, 206, 207, 208.

   Use one table row per code. Source text and severity directly from `DiagnosticDescriptor` invocations — do not invent.
3. **REFACTOR:** `PROSE_CLEAN` per file. Verify every code emitted by the analyzers has a row.

**Verification gate:**
- All four URLs resolve.
- `TERM_INDEXED "AONT200"` AND `TERM_INDEXED "AONT041"` AND `TERM_INDEXED "AONT001"`.
- Cross-check: `comm -23 <(grep -roh 'AONT[0-9]\{3\}' src/Strategos.Ontology* | sort -u) <(grep -roh 'AONT[0-9]\{3\}' docs/src/content/docs/reference/diagnostics/ | sort -u)` produces empty output (every source code is documented).
- `PROSE_CLEAN`.
**Dependencies:** T08
**Parallelizable:** Yes (one task, but distinct from C/D files)
**testingStrategy:** build, routing, content, prose, coverage

---

## Stream F — Workflow

### T22: Update `.github/workflows/docs.yml`

**Files:** `.github/workflows/docs.yml`

1. **RED:** Current workflow points at `docs/.vitepress/dist`. Push a no-op commit on the migration branch to a draft PR — workflow fails because `docs/.vitepress/dist` doesn't exist.
2. **GREEN:** Edit the workflow:
   - `Build docs` step: `run: npm run docs:build` → `run: npm run build`
   - `actions/upload-pages-artifact` `path`: `docs/.vitepress/dist` → `docs/dist`
   - Leave triggers, permissions, concurrency, deploy job untouched.
3. **REFACTOR:** `actionlint .github/workflows/docs.yml` produces no warnings (if available).

**Verification gate:** Draft PR CI run succeeds and deploys to GH Pages preview environment; the preview URL renders the new site.
**Dependencies:** T08 (so content is in place); strongly recommend after T09–T21 land so the deploy reflects the full site
**Parallelizable:** No (single shared file)
**testingStrategy:** build, routing

---

## Stream G — Acceptance

### T23: Final acceptance verification

**Files:** none (verification only)

1. **RED:** Without this task done, no evidence that §11 acceptance criteria are met.
2. **GREEN:** Walk the acceptance checklist:
   - VitePress fully removed (`grep -r vitepress docs/package.json` returns empty)
   - `astro` + `@astrojs/starlight` present
   - All four legacy content directories under `docs/src/content/docs/`
   - Ontology section appears in rendered sidebar (visual check on preview deploy)
   - Diagnostics index covers every AONT code in source (re-run T21's `comm` check)
   - `BUILD_OK` (zero warnings)
   - Pagefind canary queries from §9 all hit: `AONT213` → no result expected (not yet defined); `DispatchReadOnlyAsync`, `IOntologySource`, `polyglot`, `OntologyToolDescriptor`, `ValidationVerdict` → all hit
   - Edit link on `/strategos/guide/installation/` resolves to `github.com/lvlup-sw/strategos/edit/main/docs/src/content/docs/guide/installation.md`
   - Issue #32 confirmed off 2.5.0 milestone
3. **REFACTOR:** Capture findings in PR description.

**Verification gate:** All bullets pass.
**Dependencies:** T01, T22, T09–T21
**Parallelizable:** No
**testingStrategy:** build, routing, content, prose, coverage

---

## Notes on plan-coverage checks

The `exarchos_orchestrate({ action: "check_plan_coverage" })` and `action: "spec_coverage_check"` actions are designed for code projects with test files in TypeScript/C#. This plan's "tests" are CLI verification gates against the build output, not test files in a discoverable test runner. **Expected behavior:** these checks will likely return `passed: false` because they look for test file paths the implementer cannot produce.

Mitigation: implementers should treat the verification gate column as the test specification. The plan-review delta check should map each design § to a task by reading the spec traceability table above, not by counting test files.

If the convergence-tooling team wants this kind of docs work first-class, the orchestrate actions would need a `docs-verification-gates` flavor — out of scope for this plan.

## Task summary

| Task | Stream | Parallel? | Files | Depends on |
|---|---|---|---|---|
| T01 | A | yes | gh CLI | — |
| T02 | A | no | `docs/package.json` | — |
| T03 | A | no | `docs/astro.config.mjs` | T02 |
| T04 | A | no | `docs/src/content.config.ts` | T03 |
| T05 | A | no | (verify) | T04 |
| T06 | B | no | `git mv` | T05 |
| T07 | B | no | links | T06 |
| T08 | B | no | (verify) | T07 |
| T09 | C | yes | guide/ontology/index.md | T08 |
| T10 | C | yes | guide/ontology/similarity-search.md | T08 |
| T11 | C | yes | guide/ontology/text-chunking.md | T08 |
| T12 | C | yes | guide/ontology/polyglot-descriptors.md | T08 |
| T13 | C | yes | guide/ontology/ontology-sources.md | T08 |
| T14 | C | yes | guide/ontology/validation.md | T08 |
| T15 | C | yes | guide/ontology/read-only-dispatch.md | T08 |
| T16 | C | yes | guide/ontology/mcp-integration.md | T08 |
| T17 | D | yes | reference/ontology/index.md + api/ (3 files) | T08 |
| T18 | D | yes | reference/ontology/api/ (2 files) | T08 |
| T19 | D | yes | reference/ontology/api/source.md + graph-versioning.md | T08 |
| T20 | D | yes | reference/ontology/npgsql.md | T08 |
| T21 | E | yes | reference/diagnostics/ (4 files) | T08 |
| T22 | F | no | `.github/workflows/docs.yml` | T08 (or after C/D/E) |
| T23 | G | no | (verify) | T01, T22, all of C/D/E |

**23 tasks total.** Parallel capacity at peak (after T08): 13 tasks are parallel-safe (T09–T21).

## Dispatch policy

**Max concurrent worktrees: 3.** Even though T09–T21 are pairwise independent (each touches distinct files), the delegate phase dispatches at most three teammates at a time. The remaining tasks queue against the 3-slot pool and pick up as slots free.

Rationale: smaller blast radius per review cycle, less local resource pressure (each worktree pulls a fresh `node_modules` for the docs build), and reviewable PR commits land in a predictable order rather than a thundering herd of parallel updates.
