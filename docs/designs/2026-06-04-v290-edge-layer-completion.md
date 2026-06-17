# v2.9.0 Ontology Edge Layer — Completion Bundle

**Date:** 2026-06-04 · **Epic:** #116 · **Closes the milestone:** #120, #121, #122, #123 (+ #114 via #120) · **Folds in:** #128
**Workflow:** `v290-edge-layer-completion` · **Supersedes-for-scope:** [`2026-06-02-ontology-edge-layer.md`](2026-06-02-ontology-edge-layer.md) (DR-5..DR-9 originate there) · **Foundation:** [`2026-06-03-ontology-edge-foundation.md`](2026-06-03-ontology-edge-foundation.md) (the first four DRs, merged in #127)

## Problem Statement

Issue #127 shipped the in-memory edge foundation — object identity, the `RelateAsync` relate-store with instance-anchored traversal, and `Association<T>` authoring (epic #116's first four requirements, merged). The v2.9.0 milestone still needs the remaining five requirements — and review of #127 surfaced #128, a latent identity bug that turns out to be the structural keystone for the rest.

This bundle completes the milestone in one delegation: remove the schema-only edge surface (DR-5), enforce association cardinality (DR-6), back the model in Postgres (DR-7/8), validate the CLR-free path (DR-9), and fix the traversal-identity defect that DR-7/8 and DR-9 both depend on (DR-10 / #128).

## The keystone (#128) and dependency spine

Today traversal re-derives the target descriptor from the CLR type at each hop: `ObjectSet.TraverseLink` (`ObjectSet.cs:69`) captures only `typeof(TLinked)`, and the Postgres provider resolves identity via `ObjectTypeNamesByType[typeof(T)]`, **throwing on multi-registration** (`PgVectorObjectSetProvider.cs:131,145`). A `SymbolKey`-only descriptor has no CLR type to look up at all. So DR-7/8's join lowering cannot be INV-8-correct, and DR-9's polyglot corpus is exactly what exposes the break. Fix identity-flow first; everything else builds on it.

```text
DR-10 (#128) identity flows from the graph ─┬─► DR-7/8 Npgsql join lowering (#122) ─► DR-9 polyglot validation, both providers (#123)
                                            └─► in-memory traversal corrected
DR-5 footgun removal + diagnostic (#120) ──┐
DR-6 cardinality analyzer (#121) ──────────┘  (independent track — parallelizable; DR-6 also hosts DR-10's override diagnostic)
```

## Invariant constraints (from `/strategos-design-invariants`, verdict: conditional)

| Invariant | Constraint this design adopts |
|---|---|
| **INV-8** (HIGH) | Traversal identity flows from the `LinkDescriptor` (`TargetTypeName` / `TargetSymbolKey`), never from `typeof` at a hop. Providers consume the resolved descriptor name. SymbolKey-only path is a first-class test target, not an afterthought. |
| **INV-5** (HIGH) | New diagnostics take the **next free monotonic AONT id verified against `OntologyDiagnosticIds.cs`** (live ceiling **AONT208**) — the tickets' `AONT037`/`AONT041` are stale. No existing id is removed/renumbered; removing `IEdgeBuilder` etc. is a PublicAPI break recorded in `PublicAPI.Unshipped.txt` + CHANGELOG **Cross-product breaking changes**. |
| **INV-2** (HIGH) | DR-7/8 uses **raw Npgsql + pgvector only** — no Marten/Wolverine in `Strategos.Ontology*` (baseline is zero). DR-5/DR-6 stay **analyzers**, never `IIncrementalGenerator`. |
| **INV-6 / INV-7** | New edge/junction/SQL-mapping types are `sealed` records with `init`-only members; extend the `InvariantGuardTests` sealed-guard to them. |
| **INV-3** | DR-5's edit to `OntologyExploreTool` preserves the `_meta` envelope + `OutputSchema`. |

## Chosen Approach

The bundle scope (all four remaining DRs + #128 in one delegation) is settled. The load-bearing design fork is **how traversal carries descriptor identity** — the INV-8 keystone (#128) that DR-7/8 and DR-9 both depend on.

### Option 1: Link-declared target resolution (simple but limited)

**Approach:** Traversal resolves the target descriptor from the `LinkDescriptor`'s declared target name; `typeof(TLinked)` is kept only for the generic return shape.

**Pros:**
- INV-8-correct by construction; a `SymbolKey`-only target "just works"
- No public API change

**Cons:**
- A link whose declared target is itself multi-registered/polymorphic can't be disambiguated by the link alone

### Option 2: Explicit `descriptorName` threaded through the expression tree (flexible but complex)

**Approach:** Add an optional `descriptorName` to `TraverseLinkExpression`, exposed via `TraverseLink<TLinked>("role", descriptorName)`; the caller disambiguates each hop.

**Pros:**
- Handles polymorphic / multi-registered targets explicitly
- Symmetric with the existing `RootExpression(typeof(T), descriptorName)`

**Cons:**
- Pushes disambiguation onto every caller; omitting it silently regresses to the #128 bug
- New public surface (PublicAPI baseline churn)

### Option 3: Hybrid — link-declared default + explicit override (SELECTED)

**Approach:** Option 1 is the default resolution; Option 2's explicit `descriptorName` is an override for genuinely polymorphic links; a new analyzer diagnostic turns the ambiguous-without-override case into a compile error.

**Pros:**
- Common path is INV-8-correct with zero caller burden (closes Option 1's gap)
- Keeps an escape hatch for polymorphic targets (Option 2's strength)
- Mirrors the existing root precedent (least surprising) and converts the failure mode from a silent wrong-partition read into a compile diagnostic — the INV-5-correct tier, riding DR-6's analyzer work

**Cons:**
- Slightly more surface than Option 1; the override relies on the analyzer nudge to prevent silent omission

**Selected mechanics:**
- **Default (correct-by-construction):** `TraverseLink<TLinked>("role")` resolves the target from the source descriptor's `LinkDescriptor` for `"role"` — `TargetTypeName` for hand-authored, `TargetSymbolKey` for ingested. `typeof(TLinked)` shapes only the generic result type.
- **Override (escape hatch):** `TraverseLink<TLinked>("role", descriptorName)` + optional `TargetDescriptorName` on `TraverseLinkExpression`.
- **Guard (earliest tier, INV-5):** a new AONT diagnostic fires when a traversal targets an ambiguously-registered descriptor without an override.

---

## Technical Design

The requirements below constitute the technical design. Each `DR-N` is a provenance anchor with concrete, testable acceptance criteria; `/exarchos:plan` traces tasks to them.

## Requirements

### DR-10 — Traversal identity flows from the graph (keystone; #128)
Resolve hop targets from the ontology graph, not CLR reflection, per Approach C.

**Acceptance criteria:**
- `TraverseLinkExpression` carries an optional `TargetDescriptorName`; when absent, both evaluators resolve the target from the source descriptor's `LinkDescriptor` (`TargetTypeName`, falling back to `TargetSymbolKey`). No `typeof(TLinked)` participates in identity/partition resolution on either provider.
- A CLR type registered under two descriptors, traversed after a hop, routes to the **correct** partition/table (direct #128 regression; today it throws or mis-routes).
- A `SymbolKey`-only target traverses end-to-end with **zero reflection** on the hop, in both the in-memory and Npgsql providers.
- Traversing a link whose declared target is ambiguously multi-registered **without** an override is reported at the call site by the new AONT diagnostic (see DR-6), not at runtime.
- In-memory and Npgsql produce identical results for the same polyglot corpus (parity).

### DR-5 — Footgun removal + AONT diagnostic (#120, closes #114)
Remove the schema-only edge-properties surface; steer authors to `Association<T>`.

**Acceptance criteria:**
- `IEdgeBuilder`, `EdgeBuilder`, `ManyToMany<T>(name, Action<IEdgeBuilder>)`, `LinkDescriptor.EdgeProperties`, `RequiredEdgeProperty`/`ExternalLinkExtensionPoint.RequiredEdgeProperties`, and their `OntologyGraphHasher` + `OntologyExploreTool` references are removed; the solution builds.
- A new stable AONT diagnostic (**next free id, verified against `OntologyDiagnosticIds.cs` — not `AONT037`**) errors on any residual edge-property authoring, with a fix-it message naming `Association<T>` and this design.
- `PublicAPI.Unshipped.txt` updated; CHANGELOG **Cross-product breaking changes** records the removed surface; #114 closes on merge.
- `OntologyExploreTool` retains its `_meta` envelope + `OutputSchema` after the edit (INV-3).

### DR-6 — Association endpoint-cardinality analyzer rule (#121)
An association's endpoints must form a valid reified relation (many-to-one into the association object). Also hosts DR-10's ambiguity diagnostic.

**Acceptance criteria:**
- A stable AONT diagnostic (next free id, verified — not `AONT041`) flags an association whose endpoint cardinalities cannot form a valid reified relation, reported at the declaration site; ids documented in `OntologyDiagnosticIds`.
- A conformant attributed many-to-many (two many-to-one endpoints into the association) passes.
- The DR-10 ambiguous-target-without-override diagnostic is implemented in the same `OntologyDefinitionAnalyzer` pass and tested.

### DR-7/8 — Npgsql/pgvector edge tables + join-lowered traversal (#122)
Back the edge model in Postgres using raw Npgsql + pgvector; lower instance-anchored traversal to joins. (Provider is greenfield on the edge surface — no relate/junction code exists yet.)

**Acceptance criteria:**
- A pure link → a junction table (endpoint FK columns + edge id); an `Association<T>` → an object table whose endpoint columns are FKs.
- `SqlGenerator`/`ExpressionTranslator` lower instance-anchored traversal to `vertex ⋈ junction ⋈ vertex` joins, consuming the DR-10-resolved descriptor name (verified via generated SQL).
- `RelateAsync`/`UnrelateAsync` + `TraverseLink` reach parity with the in-memory provider against Postgres.
- A `pgvector` column coexists on an association table; a single query composes similarity + edge-attribute filters.
- No Wolverine/Marten reference enters `Strategos.Ontology.Npgsql` (INV-2). New row-mapping types are `sealed` `init`-only records (INV-6/7).

### DR-9 — CLR-free rationale-ontology validation (#123, coordinates #115)
Validate the edge layer end-to-end for a `SymbolKey`-only rationale ontology, against **both** providers.

**Acceptance criteria:**
- A rationale ontology (decisions/constraints + `Supersedes`/`Motivates`/`ConflictsWith` as `Association<T>` objects), declared with `SymbolKey`-only descriptors, relates → traverses → validates through the public primitives with no CLR types and no reflection.
- The same corpus runs against the in-memory **and** Npgsql providers with identical observable results (doubles as DR-10's polyglot parity test and DR-8's INV-8 regression).
- Association objects carry their own properties + lifecycle.
- Use **Refs/Part of** (not "Closes") for #115 — maintainer-only acceptance beyond the edge slice.

### DR-8 — Error handling, failure modes, edge cases (MANDATORY)
The relationship layer fails safely and identically across backends.

**Acceptance criteria:**
- Relating to a non-existent endpoint id surfaces a typed error (`RelationEndpointNotFoundException`-equivalent) — no silent dangling row — eagerly validated, consistently across in-memory and Npgsql.
- Self-loops `(x, link, x)` are permitted only when the link allows it; otherwise a typed error, never a silent drop.
- Traversal from an instance with zero relations returns an empty set, never all target-type items (#114 regression guard) — on both providers.
- An ambiguous multi-registration traversal without an override fails at compile (DR-10/DR-6 diagnostic), and — defensively — throws a typed error at runtime rather than mis-routing, never returning the wrong partition silently.

## Testing Strategy

- **DR-10 / #128 regression** (in-memory): a CLR type registered under two descriptors, traversed after a hop, asserts correct-partition routing; a `SymbolKey`-only target asserts zero-reflection traversal.
- **Cross-provider parity** (DR-9/DR-8): the `SymbolKey`-only rationale corpus is a single fixture run through *both* the in-memory and Npgsql providers, asserting identical observable relate → traverse → validate results. This one fixture simultaneously satisfies DR-8's INV-8 polyglot regression and DR-10's parity criterion.
- **Generated-SQL assertions** (DR-7/8): traversal lowering is verified by asserting the emitted `vertex ⋈ junction ⋈ vertex` SQL shape, not only result equality, so a regression to per-hop `typeof` resolution is caught structurally.
- **Analyzer tests** (DR-5/DR-6/DR-10): each new AONT diagnostic gets a positive (fires at the right span) and negative (conformant code is clean) test in `Strategos.Ontology.Generators.Tests`, mirroring `AONT037AnalyzerTests`.
- **Sealed-guard extension** (INV-6): `InvariantGuardTests` gains the new edge/junction/SQL-mapping types.
- **Npgsql DB infra:** provider tests run against a real Postgres+pgvector instance under `Strategos.Ontology.Npgsql.Tests`; gate the suite the same way the existing Node/benchmark suites are gated in publish-verify (per the repo's CI-split convention) so the default build stays green without a database.

## Out of scope (v2.9.0)

- **Bitemporal validity** — follow-on #126; the contract must not preclude layering it via the event stream later.
- **MCP serve surface for edges** — v2.10.0 epic #124.

### Known limitation — Mode-4 backend divergence on an unresolvable far-node hop (follow-up #128)

DR-8's failure-mode matrix asserts the four edge failure modes fail safely across backends. Modes 1-3 fail **identically** (same typed errors, same empty-set posture). **Mode 4 is SAFE on both backends but DIVERGES on _how_**, and the matrix tests (`EdgeFailureModeMatrixTests` / `EdgeFailureModeMatrixNpgsqlTests`) document this rather than overclaiming parity:

- For an **unresolvable far-node hop target** (link with no `TargetTypeName`/`TargetSymbolKey`, no override) whose source already carries a relation row, the **in-memory** evaluator degrades to the relation row's own stored `TargetDescriptor`/`TargetId` (`ResolveTargetEndpoints`) and does **not** throw.
- The **Npgsql** provider has no per-row stored target descriptor — the SQL junction table records only a surrogate `target_id` FK, not a descriptor name — so its `ResolveHopTargetDescriptorName` **refuses** the unresolvable hop with a typed `InvalidOperationException` (it derives the target table from the graph link).

Both are safe (neither mis-routes to a wrong/arbitrary partition). Closing the gap (so both throw, or both degrade) is deferred to **#128**: aligning would require either a junction-table schema change to carry the row's target descriptor, or a behavioral regression in the in-memory far-node degradation — neither is in v2.9.0 scope.

## Integration Points & Delegation shape

Keystone-first, then two parallel tracks. Suggested task slicing for `/exarchos:plan`:

| Task | DR | Issue | Depends on | Surface |
|---|---|---|---|---|
| T1 | DR-10 | #128 | — | `Strategos.Ontology` (expression tree, both evaluators), in-memory tests |
| T2 | DR-6 | #121 | T1 (ambiguity diagnostic) | `Strategos.Ontology.Generators` analyzer + tests |
| T3 | DR-5 | #120 | — (parallel) | `Strategos.Ontology` + `.Generators` + `.MCP`, PublicAPI, CHANGELOG |
| T4 | DR-7/8 | #122 | T1 | `Strategos.Ontology.Npgsql` (+ DB test infra) |
| T5 | DR-9 + DR-8 | #123 | T1, T4 | `Strategos.Ontology(.Npgsql).Tests` cross-provider corpus |

T3 has the widest blast radius (PublicAPI break) but no code dependency on the keystone — it can land first or in parallel. T4 is the heaviest. T5 is the milestone's proof-of-correctness and runs last.

## Open Questions

1. **PR shape** — one stacked PR per task (T1→T5) or a single bundle PR? The plan phase decides; the keystone (T1) merging first lets T2/T4 rebase onto a corrected traversal. Leaning stacked, T1 at the base.
2. **DR-10 override ergonomics** — should the `descriptorName` override also accept a `SymbolKey` directly (not just a descriptor name string) for fully-polyglot call sites, or is the descriptor-name string sufficient since the graph maps `SymbolKey → name`? Resolve during T1 design.
3. **Npgsql edge-id strategy** — surrogate edge id (DB-generated) vs. deterministic composite of `(srcId, link, tgtId, associationId)` to mirror the in-memory idempotency key. The in-memory store keys on the composite; parity argues for the composite as the natural key. Confirm in T4.
4. **Exact AONT ids** — assigned at implementation against the live `OntologyDiagnosticIds.cs` (ceiling AONT208); whether the new edge/association diagnostics open a dedicated band or continue from AONT209 is a generators-team call in T2/T3.
