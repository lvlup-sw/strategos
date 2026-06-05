# Implementation Plan — v2.9.0 Ontology Edge Layer Completion

**Design:** [`docs/designs/2026-06-04-v290-edge-layer-completion.md`](../designs/2026-06-04-v290-edge-layer-completion.md)
**Epic:** #116 · **Issues:** #120, #121, #122, #123 (+ #114 via #120) · **Folds in:** #128
**Workflow:** `v290-edge-layer-completion`

## Iron Law

NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST. Every task starts `[RED]`, states the expected failure, then `[GREEN]` minimum code, then `[REFACTOR]`.

> **Test invocation (TUnit):** `dotnet test --filter` does NOT work in this repo. Run a project's suite with `dotnet run --project <proj> -c Debug`, and filter with `-- --treenode-filter "/*/*/*/Method_*"`.

## Diagnostic-ID note (INV-5)

The tickets cite `AONT037`/`AONT041` — **stale**. Live ceiling is **AONT208**. Provisional next-free ids below are `AONT209/210/211`; the implementer **verifies next-free against `OntologyDiagnosticIds.cs` at GREEN time** and documents each id there. No existing id is removed/renumbered.

## Traceability matrix (DR-N → tasks)

| DR | Requirement | Tasks | Issue |
|----|-------------|-------|-------|
| DR-10 | Traversal identity flows from the graph (keystone) | T1, T2, T3 | #128 |
| DR-6 | Endpoint-cardinality analyzer + DR-10 ambiguity diagnostic | T4, T5 | #121 |
| DR-5 | Remove `EdgeProperties` + AONT migration diagnostic | T6, T7 | #120 (closes #114) |
| DR-7/8 | Npgsql edge tables + join-lowered traversal | T8, T9, T10, T11 | #122 |
| DR-9 | CLR-free rationale validation (both providers) | T12, T13 | #123 |
| DR-8 | Error handling / failure modes (cross-provider) | T14 | #122/#123 |
| INV-6 | Sealed-guard extension | T15 | — |

## Parallelization

```
Group 1 (start immediately, parallel):
  ├─ Track A: T1 → T2 → T3        (DR-10 keystone — blocks Tracks D, E, and T5)
  └─ Track C: T6 → T7            (DR-5 removal — independent; widest blast radius)

Group 2 (after Track A):
  ├─ Track B: T4 (parallel) ; T5 (needs A)
  └─ Track D: T8 → {T9, T10} → T11

Group 3 (after Tracks A + D):
  └─ Track E: T12 → T13 → T14 ; then T15 (sealed-guard)
```

Worktree isolation: Tracks A, C, D each touch disjoint projects (`Strategos.Ontology` / `Strategos.Ontology.Generators`+`.MCP` / `Strategos.Ontology.Npgsql`) and are worktree-parallel-safe. Track E is integration and runs last.

---

## Track A — DR-10: Traversal identity flows from the graph (keystone; #128)

### Task 1: Traversal identity flows from the graph — carry an explicit target descriptor name
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-10 · **testingStrategy:** unit (propertyTests: no, benchmarks: no)

1. [RED] `TraverseLinkExpression_WithDescriptorNameOverride_CarriesTargetDescriptorName`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/TraverseLinkExpressionTests.cs`
   - Expected failure: `TraverseLink<T>(link, descriptorName)` overload and `TargetDescriptorName` property don't exist yet.
2. [GREEN] Add optional `string? targetDescriptorName = null` to `TraverseLinkExpression` (ObjectSetExpression.cs:83) and a `TraverseLink<TLinked>(string linkName, string descriptorName)` overload on `ObjectSet<T>` (ObjectSet.cs:67) — mirroring `RootExpression(type, name)`.
3. [REFACTOR] XML docs; keep the single-arg overload delegating with `null`.

**Dependencies:** None · **Parallelizable:** No (Track A head)

### Task 2: In-memory traversal resolves target from the link, not `typeof` (#128 regression)
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-10 · **testingStrategy:** unit

1. [RED] `Traverse_TargetClrTypeRegisteredUnderTwoDescriptors_RoutesToLinkDeclaredTarget`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryTraversalIdentityTests.cs`
   - Expected failure: today the evaluator re-derives the partition from `typeof(TLinked).Name`, so a CLR type registered under two descriptors mis-routes/throws.
2. [GREEN] In `InMemoryExpressionEvaluator`, resolve the hop target's descriptor name from the source descriptor's `LinkDescriptor` for the link (`TargetTypeName`, then `TargetSymbolKey`), honoring `TraverseLinkExpression.TargetDescriptorName` override when present. Remove `typeof(TLinked)`-based identity from the hop.
3. [REFACTOR] Extract a single `ResolveHopTargetDescriptor(...)` helper reused by both providers' resolution.

**Dependencies:** T1 · **Parallelizable:** No

### Task 3: SymbolKey-only target traverses with zero reflection
**Phase:** RED → GREEN · **Implements:** DR-10 · **testingStrategy:** unit

1. [RED] `Traverse_SymbolKeyOnlyTarget_ResolvesWithoutReflection`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryTraversalIdentityTests.cs`
   - Expected failure: a `ClrType = null, SymbolKey = …` target can't be found via `typeof`-keyed lookup.
2. [GREEN] Ensure `ResolveHopTargetDescriptor` reads `TargetSymbolKey` → descriptor name with no `typeof`/reflection on the hop path.

**Dependencies:** T2 · **Parallelizable:** No

---

## Track B — DR-6 analyzer + DR-10 ambiguity guard (#121)

### Task 4: Endpoint-cardinality analyzer rule
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-6 · **testingStrategy:** unit (analyzer)

1. [RED] `Analyze_AssociationWithInvalidEndpointCardinality_FiresAONT210` + `Analyze_ConformantManyToManyAssociation_DoesNotFireAONT210`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/AONT210CardinalityAnalyzerTests.cs`
   - Expected failure: no such diagnostic/rule exists.
2. [GREEN] Add rule to `OntologyDefinitionAnalyzer`; register next-free id (provisional `AONT210`, verify) in `OntologyDiagnosticIds.cs` + descriptor in `OntologyDiagnostics.cs`. Flags an association whose endpoints can't form a valid many-to-one reified relation, at the declaration site.
3. [REFACTOR] Mirror the `AONT041` enforcement style.

**Dependencies:** None (cardinality is independent of Track A) · **Parallelizable:** Yes (with Track A/C)

### Task 5: Ambiguous-traversal-without-override diagnostic (DR-10 guard)
**Phase:** RED → GREEN · **Implements:** DR-10, DR-6 · **testingStrategy:** unit (analyzer)

1. [RED] `Analyze_TraverseLinkToMultiRegisteredTargetWithoutOverride_FiresAONT211` + `Analyze_TraverseLinkWithDescriptorNameOverride_DoesNotFireAONT211` + `Analyze_TraverseLinkToSingleRegisteredTarget_DoesNotFireAONT211`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/AONT211AmbiguousTraversalTests.cs`
   - Expected failure: rule absent.
2. [GREEN] Add rule (provisional `AONT211`, verify) in the same analyzer pass: a `TraverseLink<TLinked>` whose declared link target is ambiguously multi-registered AND no `descriptorName` override is supplied reports at the call site.

**Dependencies:** T1 (override surface), T4 (analyzer scaffolding) · **Parallelizable:** No

---

## Track C — DR-5 footgun removal + diagnostic (#120, closes #114)

### Task 6: Residual edge-property authoring diagnostic + surface removal
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-5 · **testingStrategy:** unit (analyzer) + build

1. [RED] `Analyze_ResidualEdgePropertyAuthoring_FiresAONT209WithAssociationFixit`
   - File: `src/Strategos.Ontology.Generators.Tests/Analyzers/AONT209EdgePropertyRemovalTests.cs`
   - Expected failure: diagnostic absent; `IEdgeBuilder` still compiles.
2. [GREEN] Add stable diagnostic (provisional `AONT209`, verify) with fix-it message naming `Association<T>` + the design. Remove `IEdgeBuilder`, `EdgeBuilder`, `ManyToMany<T>(name, Action<IEdgeBuilder>)`, `LinkDescriptor.EdgeProperties`, `RequiredEdgeProperty`/`ExternalLinkExtensionPoint.RequiredEdgeProperties`, and their `OntologyGraphHasher` references; solution builds.
3. [REFACTOR] Update `PublicAPI.Unshipped.txt`; add CHANGELOG **Cross-product breaking changes** entry (the currently-`_(none this release)_` section). #114 closes on merge.

**Dependencies:** None · **Parallelizable:** Yes (own projects; widest blast radius — land early)

### Task 7: `OntologyExploreTool` keeps its MCP envelope after edge-prop removal
**Phase:** RED → GREEN · **Implements:** DR-5 (INV-3) · **testingStrategy:** unit

1. [RED] `ExploreTool_AfterEdgePropertyRemoval_RetainsMetaEnvelopeAndOutputSchema`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyExploreToolTests.cs`
   - Expected failure: edits to drop edge-property refs must not strip `_meta`/`OutputSchema`.
2. [GREEN] Remove edge-property references from `OntologyExploreTool` while preserving `_meta` + `OutputSchema`.

**Dependencies:** T6 · **Parallelizable:** No

---

## Track D — DR-7/8 Npgsql edge tables + join-lowered traversal (#122)

### Task 8: Edge-table + association-object schema (DDL)
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-7 · **testingStrategy:** unit (SQL-shape)

1. [RED] `Schema_PureLink_EmitsJunctionTableWithEndpointFksAndEdgeId` + `Schema_Association_EmitsObjectTableWithFkEndpointColumns`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorEdgeSchemaTests.cs`
   - Expected failure: no edge/junction DDL emitter exists (greenfield on the edge surface).
2. [GREEN] Generate junction table (endpoint FK cols + edge id) for a pure link; object table with FK endpoint columns for an `Association<T>`. New row-mapping types are `sealed` `init`-only records (INV-6/7); raw Npgsql only, no Marten (INV-2).
3. [REFACTOR] Share snake_case/type mapping with existing `TypeMapper`.

**Dependencies:** T1 · **Parallelizable:** Yes (own project, after Track A)

### Task 9: `RelateAsync`/`UnrelateAsync` parity against Postgres
**Phase:** RED → GREEN · **Implements:** DR-7, DR-8 · **testingStrategy:** integration (DB-gated)

1. [RED] `Relate_StoresRow_AndUnrelate_RemovesRow_WithEagerEndpointValidation`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorRelateTests.cs`
   - Expected failure: provider has no relate/unrelate.
2. [GREEN] Implement `IObjectSetWriter` relate/unrelate on `PgVectorObjectSetProvider`; relating to a non-existent endpoint surfaces a typed error consistent with the in-memory `RelationEndpointNotFoundException` (DR-8).

**Dependencies:** T8 · **Parallelizable:** No

### Task 10: Join-lowered instance-anchored traversal
**Phase:** RED → GREEN → REFACTOR · **Implements:** DR-7, DR-10 · **testingStrategy:** unit (SQL-shape) + integration

1. [RED] `Traverse_InstanceAnchored_LowersToVertexJunctionVertexJoin` (asserts generated SQL shape) + `Traverse_ZeroRelations_ReturnsEmpty`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorTraversalTests.cs`
   - Expected failure: `ExpressionTranslator` doesn't lower `TraverseLinkExpression`.
2. [GREEN] `SqlGenerator`/`ExpressionTranslator` lower traversal to `vertex ⋈ junction ⋈ vertex`, consuming the DR-10-resolved descriptor name (the shared `ResolveHopTargetDescriptor`), never `typeof`. Reuse Track A's resolver so multi-registration/SymbolKey-only route correctly.
3. [REFACTOR] De-dup join construction.

**Dependencies:** T8, T2 · **Parallelizable:** No (sibling of T9 once T8 lands)

### Task 11: `pgvector` coexists on an association table
**Phase:** RED → GREEN · **Implements:** DR-7 · **testingStrategy:** integration (DB-gated)

1. [RED] `Query_AssociationWithPgVectorColumn_ComposesSimilarityAndEdgeAttributeFilter`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorAssociationSimilarityTests.cs`
   - Expected failure: association tables don't yet carry a vector column / composed query.
2. [GREEN] Allow a `pgvector` column on an association object table; compose similarity + edge-attribute filters in one query.

**Dependencies:** T8 · **Parallelizable:** No

---

## Track E — DR-9 + DR-8 cross-provider polyglot validation (#123)

### Task 12: CLR-free rationale ontology, in-memory
**Phase:** RED → GREEN · **Implements:** DR-9 · **testingStrategy:** integration

1. [RED] `RationaleOntology_SymbolKeyOnly_RelatesTraversesValidates_InMemory`
   - File: `src/Strategos.Ontology.Tests/Integration/RationaleOntologyEdgeTests.cs`
   - Expected failure: no rationale corpus exists; exercises decisions/constraints + `Supersedes`/`Motivates`/`ConflictsWith` as `Association<T>` with `SymbolKey`-only descriptors, no reflection.
2. [GREEN] Add the corpus fixture + any minimal primitive generalization needed for the SymbolKey-only relate→traverse path.

**Dependencies:** T3 · **Parallelizable:** No

### Task 13: Cross-provider parity (in-memory ≡ Npgsql)
**Phase:** RED → GREEN · **Implements:** DR-9, DR-10 · **testingStrategy:** integration (DB-gated)

1. [RED] `RationaleOntology_SameCorpus_InMemoryAndNpgsql_ProduceIdenticalResults`
   - File: `src/Strategos.Ontology.Npgsql.Tests/Integration/RationaleOntologyParityTests.cs`
   - Expected failure: corpus not wired to the Npgsql provider.
2. [GREEN] Run the Task-12 corpus through the Npgsql provider; assert identical observable relate→traverse→validate results.

**Dependencies:** T12, T10, T11 · **Parallelizable:** No

### Task 14: DR-8 cross-provider failure-mode matrix
**Phase:** RED → GREEN · **Implements:** DR-8 · **testingStrategy:** integration

1. [RED] One test per failure mode, parameterized across both providers:
   - `Relate_NonExistentEndpoint_ThrowsTypedError_BothProviders`
   - `SelfLoop_WhenLinkDisallows_ThrowsTypedError_NeverSilentDrop_BothProviders`
   - `Traverse_ZeroRelations_ReturnsEmpty_NotAllTargets_BothProviders` (#114 guard)
   - `Traverse_AmbiguousMultiRegistrationWithoutOverride_ThrowsAtRuntime_NeverMisroutes`
   - File: `src/Strategos.Ontology.Tests/Integration/EdgeFailureModeMatrixTests.cs`
   - Expected failure: some modes not yet enforced uniformly across providers.
2. [GREEN] Close any parity gaps so both providers fail identically.

**Dependencies:** T13 · **Parallelizable:** No

### Task 15: Extend sealed-guard to new edge/provider types (INV-6)
**Phase:** RED → GREEN · **Implements:** INV-6 · **testingStrategy:** unit

1. [RED] Add the new junction/edge-table/SQL-mapping types to the `EdgeStoreTypes_AreSealed`-style list.
   - File: `src/Strategos.Ontology.Tests/InvariantGuardTests.cs` (+ an Npgsql-side guard if the types live there)
   - Expected failure: new types not yet covered.
2. [GREEN] Confirm each new type is `sealed`; fix any that aren't.

**Dependencies:** T8 · **Parallelizable:** Yes (after T8)

---

## CI / test-gating notes

- **DB-gated tests** (T9, T11, T13, and the Npgsql half of T14) require a Postgres+pgvector instance under `Strategos.Ontology.Npgsql.Tests`. Gate them out of the default publish-verify the same way the Node-toolchain/benchmark suites are skip-patterned (`038635d`), and run them in a dedicated Postgres-provisioned job so the default build stays green without a database.
- **Unit/SQL-shape tests** (T1–T8, T10 shape assertions, T12, T15) run in the normal suite with no DB.

## Out of scope (tracked, not planned here)

- Bitemporal validity → #126. MCP serve surface for edges → v2.10.0 epic #124.
