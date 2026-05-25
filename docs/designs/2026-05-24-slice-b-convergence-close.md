# Slice (B) Convergence Close — AGWF single-source catalog + builder API-stability gate

- **Date:** 2026-05-24
- **Milestone:** Strategos 2.8.0 — Cross-product schema substrate
- **Closes:** #52 (AGWF catalog), #51 (API stability) → satisfies the #54 slice-B close-gate
- **Workflow:** `slice-b-convergence-close`
- **Lenses applied:** `/axiom:design` (DIM-1..8), `/strategos-design-invariants` (INV-1..8)

## 1. Problem

Slice (A) — Strategos.Contracts 0.2.0 — shipped (#99, #101). Slice (B), the Workflow
Builder convergence that unblocks exarchos #1256, has two children still open. Both are
*drift-control* deliverables: they turn loosely-held conventions into mechanically-enforced
cross-product contracts.

- **#52 — AGWF diagnostic codes are not single-sourced.** The codes live only in
  `src/Strategos.Generators/Diagnostics/WorkflowDiagnostics.cs` as hand-authored
  `DiagnosticDescriptor` literals, mirrored informally in tests and docs. exarchos #1256 (T6)
  needs a 1:1 mapping target it can consume to generate a TypeScript enum.
- **#51 — the seven builder interfaces are an unguarded public surface.** exarchos's
  `strategos-api-mirror.test.ts` parses their signatures. A breaking change on the Strategos
  side is invisible until exarchos CI fails downstream. There is **no** `PublicApiAnalyzers`
  wiring in the repo today (the `Strategos.Identity.Abstractions` `PublicAPI.*.txt` files are
  empty vestigial stubs).

Both are the milestone's "TypeSpec is the single canonical source / fail-closed on drift"
thesis applied to the last two un-converged surfaces.

## 2. Ground-truth corrections (issues are stale)

The design is seeded from the repo, not the issue prose:

- **AGWF count.** #52 says "AGWF001–AGWF014" (14 codes). Reality: **10 defined codes** —
  `AGWF001, 002, 003, 004, 009, 010, 012, 014, 015, 016` — with gaps (005–008, 011, 013 never
  shipped) and two codes *past* 014. The catalog is seeded from these 10 exact IDs. INV-5
  forbids renumbering, so gaps are preserved as gaps; the catalog enumerates what exists.
- **PublicAPI tooling.** #51 implies a "proven pattern." It is not wired anywhere. #51 wires
  `Microsoft.DotNet.PublicApiAnalyzers` from zero (package ref in `Directory.Packages.props`,
  per-project opt-in, populated baseline) — the empty Identity stubs are not a precedent to
  copy.

## 3. Constraints

- **Single regeneration entry point.** `scripts/contracts-codegen.sh` is the one codegen path
  (`tsp compile` → JSON Schema → `Strategos.Contracts.Codegen` emits `Generated/*.g.cs`).
  `contracts-codegen-guard.yml` already runs it and `git diff --exit-code`s the output. #52
  must ride this exact mechanism, not add a parallel generator.
- **Diagnostic IDs are public contract (INV-5).** The 10 existing IDs, their severities, and
  message semantics must survive byte-for-byte. Adding a code = minor bump; renaming/removing =
  major bump.
- **Roslyn analyzer stays the single reporting path (INV-5).** The catalog is a *data source*
  for descriptors, not a fourth validation tier. `WorkflowDiagnostics.cs` keeps reporting.
- **netstandard2.0 generator boundary.** AGWF descriptors are consumed by the generator
  assembly (`src/Strategos.Generators`, netstandard2.0). Generated C# must compile there.
- **#51 PAT dependency.** The cross-repo auto-issue Action needs `EXARCHOS_ISSUES_PAT` (a
  fine-grained PAT, `issues:write` on `lvlup-sw/exarchos`) — provisioned out-of-band. Decision:
  **build the Action now, gate activation on the secret**, ship a mocked-`gh` local dry-run in
  CI, run the live cross-repo dry-run out-of-band before closing #51.

## 4. Consumers & success

- **exarchos #1256 (T6):** consumes `agwf-catalog.json`, derives a TS enum that round-trips
  *by name* against the C# `AgwfCode` enum.
- **exarchos `strategos-api-mirror.test.ts` (R4):** parses the 7 builder signatures; the
  `PublicAPI.Shipped.txt` baseline is the thing it mirrors, and the auto-issue is what tells
  exarchos to re-baseline.
- **Success = the #54 acceptance gate green:** all four slice-B children closed, 0.2.0 round-trip
  test green, api-mirror test green against the baseline.

## 5. Design — Part A: AGWF single-source catalog (#52)

### 5.1 The data-vs-schema tension (DR-1)

TypeSpec is a *type* language; the AGWF catalog is *data* (10 instances with per-code metadata).
Three ways to make `.tsp` the canonical source:

- **(a) Schema-only `.tsp` + separate JSON data file.** `AgwfCatalog.tsp` defines `AgwfEntry`
  (shape); a hand-authored `agwf-catalog.source.json` holds the 10 entries, validated against
  the emitted schema; Codegen emits the C# enum + `agwf.md` from the data file. *Two files, but
  the `.tsp` is no longer the single source — splits truth.* Rejected.
- **(b) TypeSpec `enum` + decorator metadata (chosen).** `AgwfCatalog.tsp` declares
  `enum AgwfCode { EmptyWorkflowName: "AGWF001", ... }` and attaches metadata via a small local
  decorator set (`@severity`, `@summary`, `@remediation`, `@since`). The codes *and* their
  metadata live in one `.tsp`. The Codegen tool reads the compiled TypeSpec program (the
  `@typespec/compiler` API already on the toolchain) and emits all three artifacts.
- **(c) `union` of string literals.** Loses per-member metadata attachment. Rejected.

**Chosen: (b).** It is the only option where the `.tsp` is the sole source of truth — the
milestone's stated posture (DIM-3, INV-5). Cost: a ~40-line decorator definition + an emitter
pass in `Strategos.Contracts.Codegen`. This is the load-bearing design decision; the plan should
spike the decorator-read against the compiler API first.

### 5.2 Pipeline & outputs

```text
AgwfCatalog.tsp  ──tsp compile──▶  agwf-catalog.schema.json  (shape, into schemas/)
       │                                      │
       │  (compiled program read via @typespec/compiler API)
       ▼
Strategos.Contracts.Codegen ──┬──▶  agwf-catalog.json        (data: 10 entries; NuGet content)
                              ├──▶  AgwfCode.g.cs             (C# enum, [GeneratedCode])
                              └──▶  docs/diagnostics/agwf.md  (Markdown table)
```

All three outputs land under emitter-owned paths; `contracts-codegen-guard.yml` gains the new
paths so a hand-edit fails CI. `agwf-catalog.json` is added as `<Content>` to the Contracts
package (same as existing JSON Schema content).

### 5.3 Consumer rewire (single PR)

`WorkflowDiagnostics.cs` stops hand-authoring the `id:` strings: each `DiagnosticDescriptor`
takes its `id` from `nameof`/the generated `AgwfCode` enum (e.g. `AgwfCode.EmptyWorkflowName`
→ `"AGWF001"`). The descriptors (severity/message) stay where they are — the *enum* is the
single source for the **code identity + metadata**, the descriptor stays the runtime object.
Grep gate (INV-5 / the issue's AC): `grep -rn 'AGWF0[0-9]\{2\}' src/Strategos* --include='*.cs'
| grep -v Generated/` returns zero hits.

> Open sub-decision for the plan: whether the *full* `DiagnosticDescriptor` (message/severity)
> is also generated from the catalog, or only the code identity. Recommended: generate code +
> severity + message into a `AgwfDescriptors.g.cs` factory the analyzer consumes, so the catalog
> is the single source for *all* descriptor fields, not just the ID string. This maximizes
> DIM-3 but touches more of `WorkflowDiagnostics.cs` — flagged for plan-phase sizing.

## 6. Design — Part B: builder API-stability gate (#51)

### 6.1 Analyzer baseline

- Add `Microsoft.DotNet.PublicApiAnalyzers` to `Directory.Packages.props`; reference it from the
  project owning the 7 interfaces (`src/Strategos`, scoped via `<AdditionalFiles>` so only the
  builder surface is baselined — keeping the gate signal-rich per the issue).
- Generate `PublicAPI.Shipped.txt` covering exactly the 7 interfaces +
  `IWorkflowBuilder<TState>`, `IBranchBuilder`, `ILoopBuilder`, `IForkJoinBuilder`,
  `IApprovalBuilder`, `IFailureBuilder`, `IStepConfiguration`. `PublicAPI.Unshipped.txt` starts
  empty.
- A signature change with no matching `Unshipped` entry → `RS0016`/`RS0017` → **CI fails** with
  a named remediation message (custom CI step echoing the protocol).

### 6.2 CHANGELOG protocol

Every release bumping `Strategos.Contracts` includes a `## Cross-product breaking changes`
section — present even when empty (forces author intent). Documented in `CONTRIBUTING.md`;
`IWorkflowBuilder<TState>.cs` doc-comment references the protocol.

### 6.3 Cross-repo auto-issue (fail-soft, secret-gated)

```text
push to main ──▶ public-api-drift.yml
   │  diff PublicAPI.Shipped.txt vs previous tag
   ├─ no change ──▶ no-op
   └─ changed ───▶ if secret EXARCHOS_ISSUES_PAT present:
                      gh issue create --repo lvlup-sw/exarchos
                        --label cross-product:strategos  --body <diff link>
                   else: log "PAT absent — skipping cross-repo notify" (warn, not fail)
```

The Action is **fail-soft on the secret** (absent PAT warns, never blocks `main`). A local
dry-run job runs the diff logic against a synthetic divergence with a **mocked `gh`** (asserts
the correct `--repo`/`--label`/body) so CI proves the wiring without the live token. The live
dry-run against `lvlup-sw/exarchos` is an out-of-band step recorded in #51 before close.

## 7. Sequencing & deliverable shape

Two independent sub-units in one workflow; recommend **#52 first** (fully self-contained), then
**#51** (analyzer + Action). They share no files. Either can be a separate PR under the same
feature branch; the plan phase decides PR granularity. The #54 gate closes only when both land.

## 8. Invariant conformance (`/strategos-design-invariants`)

**Verdict: conditional pass** — clean by design, conditioned on the plan honoring three points.

| Invariant | Assessment |
|---|---|
| **INV-5** (stable diagnostic IDs) — HIGH | **Load-bearing here.** The 10 existing IDs/severities/messages are preserved byte-for-byte; gaps stay gaps; no renumber. Catalog is a *data source*, not a fourth validation tier — analyzer remains the single reporting path. Change-control: add=minor, rename/remove=major. ✅ *Condition:* the round-trip-by-name test (exarchos) must assert on enum member *names*, not ordinals. |
| **INV-6** (sealed-by-default) — HIGH | C# `enum` is implicitly sealed; generated descriptor factory (if 5.3 option taken) emits a `static` class. ✅ |
| **INV-3** (MCP latest spec) | Untouched — no MCP surface in scope. ✅ |
| **INV-1** (workflows lower via Wolverine+Marten SG) | Untouched — builder *signatures* are baselined, not their lowering. The PublicAPI gate must baseline only the 7 interfaces, not leak SG-internal types. ✅ *Condition:* `<AdditionalFiles>` scoping verified. |
| **INV-8** (polyglot identity) | N/A to this slice. |

**axiom (`/axiom:design`) highlights:** DIM-3 (Contracts) is the spine of both parts — single
source of truth + fail-closed gates. DIM-5 (Hygiene) — #52 removes the parallel literal surface
(grep gate). DIM-7 (Observability) — the auto-issue makes cross-repo drift a visible signal.
DIM-6 (Architecture) — clean authoring/consumption seam; no parallel distribution surface.

*Condition for full pass (plan phase):* (1) spike the TypeSpec decorator-read against the
compiler API before committing to DR-1(b); (2) decide 5.3 scope (ID-only vs full-descriptor
generation); (3) verify `<AdditionalFiles>` scopes the PublicAPI baseline to the 7 interfaces.

## 9. Out of scope

- #100 (bidirectional `FromContract()`) — deferred; no 0.2.0 consumer needs it.
- #63/#65/#66 (semantic-merge-queue S-series) — distinct epic.
- T4 cross-runtime dispatch — later milestone.
