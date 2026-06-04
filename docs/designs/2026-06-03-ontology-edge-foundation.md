# Design: Ontology Edge Foundation — instance identity + in-memory relationship layer (v2.9.0)

- **Date:** 2026-06-03
- **Milestone:** v2.9.0
- **Design input:** [`docs/designs/2026-06-02-ontology-edge-layer.md`](2026-06-02-ontology-edge-layer.md) (parent epic), [`docs/research/2026-06-02-optimal-edge-api.md`](../research/2026-06-02-optimal-edge-api.md) (discovery)
- **Realizes:** DR-1, DR-2, DR-3, DR-4 and the in-memory slice of DR-8 from the parent epic. **Coordinates:** #114, #115.
- **Scope:** the *foundation* bundle — core + in-memory provider only. No second provider, no analyzer rules.
- **Status:** approved for TDD planning via `/exarchos:plan`.

## Problem

The parent epic establishes the defect: the ontology advertises a property-graph affordance it does not back, and has no instance-level relationship layer at all. `ObjectSet.TraverseLink` resolves by target *type* — `InMemoryExpressionEvaluator.EvaluateTraverseLink` calls `itemResolver(link.TargetTypeName)` (`InMemoryExpressionEvaluator.cs:139`) and returns every object of that type with no source-instance filter. You cannot express "instance A relates to instance B" through any provider.

The code map surfaced a sharper sub-problem the parent doc only implied: **there is no object-instance id today.** `ObjectTypeDescriptor` carries `KeyProperty` (a `PropertyDescriptor` — name + type), but nothing reads its *value* off an instance; instance identity is `item?.ToString()` (`ObjectSet.ApplyAsync:134`), and `OntologyEvent.ObjectId` / `ActionContext.ObjectId` are ad-hoc strings produced the same way. An association row cannot reference endpoints it cannot name.

This foundation builds the smallest self-demonstrating core: a deterministic instance id, an in-memory relationship store with relate/unrelate, instance-anchored traversal that closes the #114 regression, and the `Association<T>` authoring surface — end-to-end in-memory, with nothing that requires a second provider or a new analyzer.

## Decisions (locked at ideation)

1. **Substrate = unified association-row store (Approach A).** One row shape `(srcId, linkName, tgtId, associationObjectId?)`. A pure link is a bare row; an attributed relationship promotes the row by storing an `ObjectKind.Association` object and back-referencing its id. Chosen over object-uniform (B — over-objectifies pure links, diverges from DR-7's junction-table target) and adjacency-index-primary (C — a derived structure that can drift). A is the parent design's own spectrum and pre-images the DR-7 Postgres lowering, so the in-memory and Npgsql providers converge rather than diverge.
2. **Identity is reflection-free on both paths.** CLR descriptors carry a captured key-selector delegate; `SymbolKey`-only descriptors get their accessor from the contributing `IOntologySource`. No per-call reflection anywhere (INV-8). Rejected: reflecting on `KeyProperty.Name` — it is structurally impossible on the polyglot path and is INV-8's worked-example failure verbatim.
3. **Foundation defers DR-5/DR-6/DR-7/DR-9.** Footgun removal + AONT diagnostic (DR-5), cardinality analyzer rule (DR-6), Npgsql/pgvector tables (DR-7), and rationale/CLR-free validation (DR-9) land in follow-on passes. This bundle touches **no analyzer**, so INV-5 stays dormant here — but see *Deferred* for the diagnostic-id correction it must carry.

## Approach (chosen): row substrate + reflection-free identity projector

**Identity (DR-1).** A core `IObjectIdentityProjector` with `string ProjectId(ObjectTypeDescriptor descriptor, object instance)`. The descriptor gains a reflection-free id accessor: for a CLR descriptor it is a `Func<object, object?>` captured at the key-declaration site (the lambda *is* the accessor — no reflection); for a `SymbolKey`-only descriptor it is supplied by the `IOntologySource` that contributed the descriptor, operating on that source's instance representation. Composite keys format deterministically with a reserved separator; the single-key case is the norm. The projector lives in `Strategos.Ontology` core and reaches no Marten/Wolverine (INV-2). New surface is typed on the descriptor + an identity discriminator, never `Type` (INV-8).

**Storage (DR-2).** The in-memory provider keeps the existing `_items` (`ConcurrentDictionary<string, List<object>>`) for vertices and association objects, and adds a provider-local **relate-store** keyed `(srcDescriptorName, srcId, linkName)` → an ordered list of rows `(tgtDescriptorName, tgtId, associationObjectId?)`. Rows are read in ordinal-by-id order so replay and tests are deterministic (`ConcurrentDictionary` enumeration is not — INV-7's determinism concern). `RelateAsync(srcDesc, srcId, linkName, tgtDesc, tgtId, ct)` is an idempotent row insert; `UnrelateAsync(...)` removes a matching row and is a no-op when absent. Both live on the provider's write surface alongside `StoreAsync` (`IObjectSetWriter`), keeping storage in the provider (INV-2).

**Traversal (DR-3).** `EvaluateTraverseLink` stops calling `itemResolver(link.TargetTypeName)`. Instead it materializes the upstream set to instances, projects their ids, looks up rows by `(srcId, linkName)`, collects `tgtId`s, and resolves the target objects by id. `GetObjectSet<T>().Where(t => t.Id == x).TraverseLink<U>("link")` returns only the `U`s related to `x`. When a link is backed by association objects, the traversal step can yield the association objects for attribute filtering (`.Where(c => c.Status == Active)`) before the further hop resolves the far endpoint via the row's other side.

**Authoring (DR-4).** `ObjectKind` gains `Association` (today `{ Entity, Process }`). A top-level `ontology.Association<TRel>(name, a => { a.Between<L>(lSel).And<R>(rSel); a.Property(...); a.Lifecycle(...); a.Event<…>(...); })` declares an association as an `ObjectTypeDescriptor` with `Kind = Association`, two typed endpoints, and its own properties — distinct from `ManyToMany`, which hangs off a single source type. The graph builder recognizes association objects and exposes their endpoints as edge source/destination. The descriptor, endpoint, and builder types are `sealed` (INV-6); the association object is an immutable record with no mutation surface (INV-7); endpoints carry identity via descriptors, not `typeof` (INV-8).

## Requirements

### DR-1 — Object-identity contract
A deterministic, polyglot-safe string id per object instance, resolvable by the in-memory provider from the declared key.

**Acceptance criteria:**
- Given an instance and its descriptor, `IObjectIdentityProjector.ProjectId` returns a deterministic string id from the declared key.
- A `SymbolKey`-only (CLR-free) descriptor resolves an id with **no reflection** (accessor supplied by its `IOntologySource`).
- Identical key values yield identical ids; the contract is written so the future Npgsql provider produces the same id (round-trip test seam reserved).
- No public API in this contract accepts or returns a `System.Type` to denote identity.

### DR-2 — Instance-level relationship primitive (in-memory)
A relate-store plus relate/unrelate verbs for pure links.

**Acceptance criteria:**
- `RelateAsync(srcDescriptor, srcId, linkName, tgtDescriptor, tgtId, ct)` and `UnrelateAsync(...)` create/remove an instance link on the in-memory provider.
- Rows are stored separately from object items; read order is deterministic (ordinal by id) for replay/test fidelity.
- `RelateAsync` is idempotent on a duplicate `(src, link, tgt)`; `UnrelateAsync` on a missing row is a no-op (no throw).

### DR-3 — Instance-anchored traversal
`TraverseLink` resolves by source instance, not target type.

**Acceptance criteria:**
- `GetObjectSet<T>().Where(t => t.Id == x).TraverseLink<U>("link")` returns only `U` instances related to `x` via relate-store rows.
- Traversal over an association-backed link exposes the association object's attributes for filtering before traversing to the far endpoint.
- `EvaluateTraverseLink` no longer calls `itemResolver(link.TargetTypeName)` to fetch all target-type items (the line-139 path is removed).

### DR-4 — `Association<T>` authoring + `ObjectKind.Association`
A first-class reified-relation affordance on the builder + descriptor model.

**Acceptance criteria:**
- `ontology.Association<TRel>(name, a => { a.Between<L>(…).And<R>(…); a.Property(…); … })` declares a relation object with two typed endpoints and its own properties.
- `ObjectKind` gains `Association`; the graph builder recognizes association objects and exposes their endpoints as the edge source/destination.
- An association object is an immutable, `sealed` record; no mutation surface is introduced (INV-6, INV-7).
- Relating *with attributes* stores the association object via `StoreAsync` and writes a row whose `associationObjectId` references it.

### DR-8 (in-memory slice) — Error handling and edge cases (MANDATORY)
The relationship layer fails safely in-memory; the contract fixes a posture both backends will honor.

**Acceptance criteria:**
- Relating to a non-existent endpoint id surfaces a **typed error** (not a silent dangling row). The in-memory provider validates endpoints **eagerly**; the contract documents this as the posture Npgsql's FK constraints will mirror.
- Self-loops `(x, link, x)` are permitted only when the link opts in; otherwise a typed runtime error, never a silent drop. (Promotion to an analyzer diagnostic is DR-6, deferred.)
- Traversal from an instance with zero relations returns an **empty set**, never all target-type items — the regression guard for the #114 defect.
- A `SymbolKey`-only descriptor exercises the full relate → traverse path with no reflection (polyglot regression test, INV-8).

## Invariant conformance

| Invariant | How this design satisfies it |
|---|---|
| INV-2 (ontology self-contained) | Relate-store lives in the in-memory provider; the projector lives in core. No Wolverine/Marten reference enters `Strategos.Ontology*`. |
| INV-6 (sealed by default) | New `Association<T>` descriptor, endpoint, and builder types are `sealed`; extension via `IOntologySource`, not subclassing. |
| INV-7 (immutable state) | Association objects are immutable records; relate/unrelate append/remove rows rather than mutate stored vertices; relate-store rows are read in a deterministic order for replay fidelity. |
| INV-8 (polyglot identity) | Identity via captured selector (CLR) or source-provided accessor (`SymbolKey`-only); zero per-call reflection; no `Type`-typed identity API. |
| INV-5 (stable diagnostic ids) | Dormant — this bundle adds no analyzer rule. The diagnostic-id correction is carried to the deferred DR-5/DR-6 (see below). |

## Design-dimension review (axiom DIM-1..DIM-8)

Approach A was selected after evaluating three substrates against the dimensions. Discriminators: **DIM-4** (A pre-images DR-7's junction-table lowering, minimizing in-memory↔Npgsql divergence), **DIM-1/DIM-5** (A's two stores hold *different* truths — edges vs vertices/attributes — so there is no divergent-implementation hazard, only one consistency seam at relate/unrelate). Parity items the plan must honor: **DIM-2** typed errors for the DR-8 failure modes; **DIM-7** deterministic ordered reads (the relate-store is authoritative state, not an eviction cache, so size bounds do not apply but ordering does); **DIM-8** concrete prose in the new XML docs.

## Decomposition → tasks

| Task | DR anchors | Notes |
|---|---|---|
| Object-identity projector + descriptor accessor | DR-1 | Dependency root. Reflection-free CLR + polyglot paths. |
| In-memory relate-store + `Relate/UnrelateAsync` | DR-2, DR-8 | Eager endpoint validation, idempotent insert, deterministic read order. |
| Instance-anchored `TraverseLink` | DR-3, DR-8 | Replaces the line-139 type-resolve; zero-relations empty-set regression guard. |
| `Association<T>` + `ObjectKind.Association` | DR-4 | Top-level authoring; graph builder endpoint exposure; attributed-relate seam to DR-2. |

**Dependency order:** DR-1 → DR-2 → {DR-3, DR-4}; the DR-8 criteria fold into DR-2 (relate validation) and DR-3 (traversal regression guard).

## Deferred (out of scope for this foundation)

- **DR-5** footgun removal (`IEdgeBuilder`, `ManyToMany(name, edgeConfig)`, `LinkDescriptor.EdgeProperties`, `EdgeBuilder.Property<TProp>`'s `typeof`) + AONT diagnostic.
- **DR-6** association endpoint-cardinality analyzer rule.
- **DR-7** Npgsql/pgvector junction tables + join-lowered traversal.
- **DR-9** rationale / CLR-free edge validation (coordinates #115).

**Diagnostic-id correction (carry forward to DR-5/DR-6):** the parent doc cites "next free id after AONT037" and `AONT041`. Neither matches source: `OntologyDiagnosticIds.cs` runs the standard sequence to **AONT037**, then a 200-series block to **AONT208** (polyglot graph-freeze). `AONT041` does not exist. When DR-5/DR-6 assign new ids, take the genuine next-free id against the real maximum — monotonic, never reused (INV-5).
