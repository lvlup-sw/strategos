# Ontology 2.5.0 — Documentation & Starlight Migration

**Status:** Draft
**Milestone:** Ontology 2.5.0 — Coordination Floor
**Closes:** #23
**Defers:** #32 (moved to backlog; see §3)

## 1. Context

Ontology 2.5.0 has shipped ten of twelve issues across the four planned slices (A — MCP surface; B — read-only dispatch; C — validation primitive; D — source/provenance). The remaining two open issues are #23 (docs update for the Ontology subsystem) and #32 (multi-registered CLR types in structural links).

#23 was filed against the PR-22 ontology merge and observed that the VitePress site has no Ontology section, the README does not list `Strategos.Ontology` or `Strategos.Ontology.Npgsql`, and the architecture spec where ontology primitives live (`docs/reference/platform-architecture.md` §4.14) is buried. Since #23 was filed the surface area has grown further: 2.5.0 added polyglot descriptors (#48), `IOntologySource` (#37), AONT200-series drift diagnostics (#43), `DispatchReadOnlyAsync` (#39), structured constraint feedback (#38), `ontology_validate` and blast-radius primitives (#41, #42), MCP `_meta` envelope (#40), and graph versioning (#44). None of it is yet reflected on the docs site.

We are bundling a VitePress → Starlight migration into the same PR. Starlight gives us Pagefind search out of the box, a content collection model that derives URLs from filesystem layout (which makes `git mv` a no-op for external links), `autogenerate` sidebars, and an actively maintained Astro substrate. The migration is mostly mechanical: `git mv` the four content directories into `docs/src/content/docs/`, swap the framework, and add the Ontology section.

## 2. Goals

- Land #23 with a comprehensive Ontology section covering every 2.5.0 surface (polyglot descriptors, `IOntologySource`, AONT200, validation, read-only dispatch, MCP `_meta`, graph versioning).
- Migrate the docs site from VitePress 1.5.0 to Astro + Starlight in the same PR.
- Preserve every existing URL under `https://lvlup-sw.github.io/strategos/` modulo trailing-slash policy (see §5).
- Preserve the existing GitHub Pages deployment workflow with the smallest possible diff.
- Render AONT001 through AONT200-series as a single browsable diagnostics index, the way users actually look them up (search by code).

## 3. Non-goals

- **Cross-cutting 2.5.0 surface updates outside the Ontology section.** Existing pages (`learn/`, `guide/installation`, `examples/*`) keep their current content. Adding ontology callouts elsewhere is a follow-up cleanup PR.
- **IA reorganization.** Top-level grouping (Learn / Guide / Reference / Examples) stays as-is. The Ontology section nests under Guide and Reference.
- **README package overview update.** Item 1 of #23 (adding `Strategos.Ontology*` packages to README.md) is a one-line change but technically outside this PR's scope; it ships in the same follow-up cleanup PR.
- **#32 multi-registered CLR types in links.** Deferred to backlog. The original #32 body sets "a concrete downstream use case drives the work" as an explicit acceptance criterion, and no such consumer exists today. `AONT041 MultiRegisteredTypeInLink` continues to gate the case and will surface the first real driver. #32 is moved off the 2.5.0 milestone (target: 2.6.0 or unmilestoned).
- **Versioned docs.** Starlight supports versioning later; nothing in 2.5.0 needs it.
- **Theme customization.** Default Starlight theme with the existing logo. Custom CSS is out of scope.

## 4. Migration shape — Option B (in-place + `git mv`)

The Astro/Starlight project lives at `docs/`. Markdown content moves under `docs/src/content/docs/` to follow Starlight's content collection convention. The directories that VitePress excluded from the build (`docs/designs/`, `docs/plans/`, `docs/archive/`, `docs/theory/`, plus the standalone files `design.md`, `deferred-features.md`, `diagnostics.md`, `integrations.md`, `packages.md`, `microsoft-agent-framework-workflows.md`, `workflow-library-roadmap-v2.md`) stay where they are — they were never published and continue not to be published.

**File layout after migration:**

```
docs/
├── astro.config.mjs              ← new
├── package.json                  ← updated: astro + @astrojs/starlight
├── package-lock.json             ← regenerated
├── public/
│   └── logo.svg                  ← moved from docs/public/
├── src/
│   ├── assets/                   ← new, if needed for images
│   ├── content.config.ts         ← new (docsLoader + docsSchema)
│   └── content/
│       └── docs/
│           ├── index.md          ← moved from docs/index.md
│           ├── learn/            ← git mv from docs/learn/
│           ├── guide/            ← git mv from docs/guide/ + new ontology/
│           ├── reference/        ← git mv from docs/reference/ + new ontology/ and diagnostics/
│           └── examples/         ← git mv from docs/examples/
├── designs/                      ← unchanged, not published
├── plans/                        ← unchanged, not published
├── archive/                      ← unchanged, not published
└── theory/                       ← unchanged, not published
```

VitePress files removed: `docs/.vitepress/`, the `vitepress` devDependency, the `docs:dev` / `docs:build` / `docs:preview` scripts (replaced with Astro equivalents).

**URL preservation by construction.** Starlight derives each page's slug from its path relative to `src/content/docs/`. After the move, `docs/src/content/docs/guide/installation.md` resolves to `/strategos/guide/installation/` — identical to today modulo the trailing slash. No redirect table required; no URL drift risk from config typos.

## 5. Astro & Starlight configuration

```js
// docs/astro.config.mjs
import { defineConfig } from 'astro/config'
import starlight from '@astrojs/starlight'

export default defineConfig({
  site: 'https://lvlup-sw.github.io',
  base: '/strategos/',
  trailingSlash: 'always',  // see decision below
  integrations: [
    starlight({
      title: 'Strategos',
      description: 'Deterministic, auditable AI agent workflows for .NET',
      logo: { src: './public/logo.svg' },
      favicon: '/logo.svg',
      social: [
        { icon: 'github', label: 'GitHub', href: 'https://github.com/lvlup-sw/strategos' },
      ],
      editLink: {
        baseUrl: 'https://github.com/lvlup-sw/strategos/edit/main/docs/src/content/docs/',
      },
      pagefind: true,
      sidebar: [
        { label: 'Learn', autogenerate: { directory: 'learn' } },
        {
          label: 'Guide',
          items: [
            { autogenerate: { directory: 'guide', collapsed: false } },
            { label: 'Ontology', autogenerate: { directory: 'guide/ontology' } },
          ],
        },
        {
          label: 'Reference',
          items: [
            { autogenerate: { directory: 'reference' } },
            { label: 'Ontology', autogenerate: { directory: 'reference/ontology' } },
            { label: 'Diagnostics', autogenerate: { directory: 'reference/diagnostics' } },
          ],
        },
        { label: 'Examples', autogenerate: { directory: 'examples' } },
      ],
    }),
  ],
})
```

```ts
// docs/src/content.config.ts
import { defineCollection } from 'astro:content'
import { docsLoader } from '@astrojs/starlight/loaders'
import { docsSchema } from '@astrojs/starlight/schema'

export const collections = {
  docs: defineCollection({
    loader: docsLoader(),
    schema: docsSchema(),
  }),
}
```

**Trailing-slash decision.** VitePress 1.x generates clean URLs (`/strategos/guide/installation`, no trailing slash, no `.html`). Astro defaults to `trailingSlash: 'ignore'` which leaves it to the deploy target; GitHub Pages will canonicalize to `/index.html` lookup. Setting `trailingSlash: 'always'` produces `/strategos/guide/installation/` and matches Starlight's link generation. External links to the old form will receive a 301 from GitHub Pages' default trailing-slash redirect. Verifying this in the preview deploy is on the implementation checklist (§9).

## 6. Sidebar design

Starlight has a single sidebar (no separate top nav). The four current top-level VitePress sections become four top-level sidebar groups: **Learn**, **Guide**, **Reference**, **Examples**. Each uses `autogenerate` against the corresponding directory so adding a new page is a single-file change with no config edit.

The Ontology section appears twice — once under Guide (task-oriented walkthroughs) and once under Reference (lookup material) — with a third subsection under Reference for the diagnostics index. This mirrors how 2.5.0's surface actually splits: "how do I use `ontology_validate`" is a guide question, "what fields does `ValidationVerdict` carry" is a reference question, "what does AONT213 mean" is a diagnostics question.

`autogenerate` reads sidebar order from each page's `sidebar.order` frontmatter (default: alphabetical). Pages that need a specific position get explicit ordering; the rest stay alphabetical.

## 7. Ontology section content plan

**Guide tier — `docs/src/content/docs/guide/ontology/`:**

| File | Coverage | Sources |
|---|---|---|
| `index.md` | Getting started: define types, register with `AddOntology<T>()`, query via `IOntologyQuery` | `src/Strategos.Ontology/README.md`, `platform-architecture.md` §4.14 |
| `similarity-search.md` | `ISearchable`, `ExecuteSimilarityAsync`, `IEmbeddingProvider`, pgvector setup | Existing `Strategos.Ontology.Npgsql` README + §4.14.7 |
| `text-chunking.md` | `ITextChunker`, `SentenceBoundaryChunker`, `ChunkOptions` | `src/Strategos.Ontology/Chunking/` source + spec §4.14.8 |
| `polyglot-descriptors.md` | 2.5.0 (#48): optional `ClrType`, `SymbolKey`, `LanguageId`, AONT037 | Design doc `2026-05-10-ontology-2-5-0-polyglot-ingestion.md` |
| `ontology-sources.md` | 2.5.0 (#37): `IOntologySource` extension, runtime `OntologyBuilder`, provenance | Design doc, source under `src/Strategos.Ontology/Sources/` |
| `validation.md` | 2.5.0 (#41, #42): `ontology_validate`, `ValidationVerdict`, blast-radius primitives, pattern violations | Design doc `2026-05-08-ontology-2-5-0-dispatch-validation.md` |
| `read-only-dispatch.md` | 2.5.0 (#39, #38): `DispatchReadOnlyAsync`, `.ReadOnly()` DSL, structured constraint feedback, `IActionDispatchObserver` | Same design doc |
| `mcp-integration.md` | 2.5.0 (#40): `OntologyToolDescriptor` MCP 2025-11-25 upgrade, `_meta` envelope | Design doc `2026-04-19-mcp-surface-conformance.md` |

**Reference tier — `docs/src/content/docs/reference/ontology/`:**

| File | Coverage |
|---|---|
| `index.md` | Overview — what ontology is, package map, links to guide |
| `api/ontology-query.md` | `IOntologyQuery` surface (including `GetObjectTypeNames<T>` and new 2.5.0 additions) |
| `api/object-set-provider.md` | `IObjectSetProvider`, `IObjectSetWriter`, expression types |
| `api/embedding-provider.md` | `IEmbeddingProvider`, `EmbeddingOptions` |
| `api/dispatcher.md` | `IActionDispatcher`, `DispatchReadOnlyAsync`, `IActionDispatchObserver` |
| `api/source.md` | `IOntologySource`, `OntologyBuilder`, `ProvenanceMetadata` |
| `npgsql.md` | `Strategos.Ontology.Npgsql`: `PgVectorOptions`, `EnsureSchemaAsync`, distance metrics, partitioning |
| `graph-versioning.md` | 2.5.0 (#44): `OntologyGraph.Version` hash semantics, when it changes |

**Diagnostics tier — `docs/src/content/docs/reference/diagnostics/`:**

| File | Coverage |
|---|---|
| `index.md` | Range overview: AONT001–099 (registration), 100–199 (composition + links), 200-series (drift) |
| `aont-001-aont-099.md` | Hand-authored codes; one row per code with description + fix |
| `aont-100-aont-199.md` | Composition / link diagnostics, including `AONT041` (with note that #32 may relax it) |
| `aont-200-series.md` | Drift diagnostics from #43; severity model and how the analyzer surfaces them |

Diagnostics pages are tabular rather than narrative — `code | severity | description | fix | introduced-in`. Lookup-by-code is the dominant query pattern (Pagefind search will surface them directly).

## 8. GitHub Pages workflow change

`/home/reedsalus/Documents/code/lvlup-sw/strategos/.github/workflows/docs.yml` changes are minimal:

- `cache-dependency-path: docs/package-lock.json` — unchanged (still `docs/`).
- `Build docs` step: `npm run docs:build` is replaced with `npm run build` (Astro's default), still running with `working-directory: docs`.
- `actions/upload-pages-artifact` `path`: `docs/.vitepress/dist` → `docs/dist`.

No changes to triggers, permissions, concurrency, or deploy job.

## 9. Implementation checklist

- [ ] Open a draft PR with the `git mv` commit isolated as the first commit (review-friendly)
- [ ] Add Astro + `@astrojs/starlight` to `docs/package.json`; remove `vitepress`
- [ ] Add `docs/astro.config.mjs` and `docs/src/content.config.ts` per §5
- [ ] Move `docs/public/logo.svg` if path needs adjustment
- [ ] Verify each existing URL resolves after migration (sample: `/strategos/`, `/strategos/learn/`, `/strategos/guide/installation/`, `/strategos/reference/diagnostics/`)
- [ ] Write Ontology guide pages per §7 (8 pages, derive content from cited sources — do not invent)
- [ ] Write Ontology reference pages per §7 (8 pages, mostly API skeletons sourced from XML doc comments + spec)
- [ ] Write diagnostics index pages per §7 (4 files; codes sourced from `src/Strategos.Ontology/Diagnostics/`)
- [ ] Update `.github/workflows/docs.yml` per §8
- [ ] Local build (`npm run build` in `docs/`) succeeds with zero broken-link warnings
- [ ] Pagefind search returns results for "AONT213", "DispatchReadOnlyAsync", "IOntologySource", "polyglot"
- [ ] GH Pages preview deploy verified before merge

## 10. Risks & open questions

**Trailing slash and external links.** Setting `trailingSlash: 'always'` is the cleanest Starlight default but introduces a 301 redirect for any external link that omits the slash. Low impact in practice — internal links all regenerate cleanly, and GitHub Pages handles the redirect — but worth verifying with the preview deploy.

**`ignoreDeadLinks: true` was enabled in VitePress.** That hid broken internal links during the VitePress era. Starlight will surface them as build warnings. Expect to fix a handful during migration.

**AONT code coverage completeness.** The diagnostics pages need the current code list. The source of truth is the diagnostic analyzers under `src/Strategos.Ontology/Diagnostics/`. The implementation step should grep for `Diagnostic.Create` / `DiagnosticDescriptor` to enumerate codes rather than copying from memory.

**README package overview update.** Item 1 of #23 calls for adding `Strategos.Ontology*` to the README package table. It's a three-line change and arguably belongs in this PR, but the user has explicitly scoped this bundle to "Ontology section only." Default: defer to follow-up. Open to revisiting in plan review.

**MDX vs MD.** Some guide pages may benefit from MDX (e.g., `mcp-integration.md` could embed an Astro component showing a request/response side-by-side). Default: ship as plain `.md`; convert to `.mdx` only where a component is added.

## 11. Acceptance criteria

- [ ] `docs/.vitepress/` removed; `vitepress` no longer in `package.json`
- [ ] `astro`, `@astrojs/starlight` installed; `docs/astro.config.mjs` and `docs/src/content.config.ts` present per §5
- [ ] All four legacy content directories present under `docs/src/content/docs/`
- [ ] Ontology section exists in sidebar with all eight guide pages and eight reference pages
- [ ] Diagnostics index covers AONT001 through the latest AONT200-series code, sourced from the analyzer code
- [ ] `npm run build` in `docs/` produces a working site; zero broken-link warnings (after fixing what VitePress hid)
- [ ] Pagefind returns hits for the four canary queries in §9
- [ ] `.github/workflows/docs.yml` builds and deploys; preview URL renders correctly
- [ ] Edit link on a published page resolves to the correct path under `docs/src/content/docs/`
- [ ] Issue #32 moved off Ontology 2.5.0 milestone with deferral comment

## 12. References

- Closed 2.5.0 issues defining the surface being documented: #37, #38, #39, #40, #41, #42, #43, #44, #48
- Open issues this design closes/defers: #23 (closes), #32 (defers)
- Architecture spec: `docs/reference/platform-architecture.md` §4.14
- Prior ontology design docs: `docs/designs/2026-05-08-ontology-2-5-0-dispatch-validation.md`, `docs/designs/2026-05-10-ontology-2-5-0-polyglot-ingestion.md`, `docs/designs/2026-04-19-mcp-surface-conformance.md`
- Starlight content collection conventions: `https://starlight.astro.build/manual-setup/`
- Source of AONT codes: `src/Strategos.Ontology/Diagnostics/`
