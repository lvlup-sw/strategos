# Plan: Ontology Edge Foundation (v2.9.0)

- **Design:** [`docs/designs/2026-06-03-ontology-edge-foundation.md`](../designs/2026-06-03-ontology-edge-foundation.md)
- **Iron law:** no production code without a failing test first.
- **Test framework:** TUnit. Conventions from existing suites: `[Test]`, `await Assert.That(x).IsEqualTo(...)` / `.IsNotNull()` / `.IsTypeOf<T>()` / `.Throws<T>()`; names `Type_Scenario_Outcome`.
- **Run a single test:** `dotnet test src/Strategos.Ontology.Tests -- --treenode-filter "/*/*/*/<TestName>"` (the `--filter` form does not work in this repo).
- **Dependency order:** DR-1 → DR-2 → {DR-3, DR-4}. DR-8 criteria fold into DR-2 and DR-3.

## Grounding facts (from code map)

- `IObjectTypeBuilder.Key(Expression<Func<T, object>> keySelector)` (`IObjectTypeBuilder.cs:9`) already captures the key selector; `ObjectTypeBuilder.Key` (`ObjectTypeBuilder.cs:53`) derives only `_keyProperty` (name) today and drops the expression. **DR-1 retains `keySelector.Compile()` as the reflection-free CLR id accessor.**
- `InMemoryObjectSetProvider` (`ObjectSets/InMemoryObjectSetProvider.cs:19`) is `sealed`, implements `IObjectSetProvider, IObjectSetWriter`; vertices live in `_items: ConcurrentDictionary<string, List<object>>` (line 30). **DR-2 adds a sibling relate-store field.**
- `IObjectSetWriter` (`ObjectSets/IObjectSetWriter.cs:12`) has four `Store*` methods. **DR-2 adds `RelateAsync` / `UnrelateAsync`.**
- `InMemoryExpressionEvaluator.EvaluateTraverseLink` resolves by type at `itemResolver(link.TargetTypeName)` (`InMemoryExpressionEvaluator.cs:139`). **DR-3 replaces that path.**
- `ObjectKind` (`Descriptors/ObjectKind.cs`) = `{ Entity, Process }`. **DR-4 adds `Association`.** No `Association` symbol exists anywhere yet — greenfield.

---

## Group 1 — DR-1: Object-identity projector (dependency root)

**Branch:** `feat/edge-foundation-dr1-identity`

### Task 1: Compile the existing `Key` selector into a descriptor id accessor
**Phase:** RED → GREEN → REFACTOR

1. [RED] Write test: `Key_Selector_RetainsCompiledIdAccessor`
   - File: `src/Strategos.Ontology.Tests/Builder/ObjectTypeBuilderTests.cs`
   - Expected failure: `ObjectTypeDescriptor` has no id-accessor member; only `KeyProperty` (name) is retained.
2. [GREEN] Add `Func<object, object?>? IdAccessor { get; init; }` to `ObjectTypeDescriptor`; in `ObjectTypeBuilder.Key`, store `keySelector.Compile()` (boxed to `Func<object, object?>`) alongside `_keyProperty`; assign at `ObjectTypeBuilder.cs:169`.
   - Files: `src/Strategos.Ontology/Descriptors/ObjectTypeDescriptor.cs`, `src/Strategos.Ontology/Builder/ObjectTypeBuilder.cs`
3. [REFACTOR] Accessor cached on the descriptor instance (not a static) — avoids the TUnit static-state/parallelism hazard.

**Dependencies:** None · **Parallelizable:** No (root)

### Task 2: `IObjectIdentityProjector.ProjectId` — CLR path
1. [RED] `ProjectId_ClrDescriptorWithKey_ReturnsDeterministicId` and `ProjectId_SameKeyValue_ReturnsSameId`
   - File: `src/Strategos.Ontology.Tests/Identity/ObjectIdentityProjectorTests.cs` (new dir)
   - Expected failure: type does not exist.
2. [GREEN] `IObjectIdentityProjector` + `ObjectIdentityProjector` in `src/Strategos.Ontology/Identity/`; `string ProjectId(ObjectTypeDescriptor descriptor, object instance)` invokes `descriptor.IdAccessor` and `ToString()`s the result.
3. [REFACTOR] Null-key → typed `InvalidOperationException` with descriptor name in the message (DIM-2 context).

**Dependencies:** Task 1 · **Parallelizable:** No

### Task 3: Polyglot path — `SymbolKey`-only accessor with no reflection
1. [RED] `ProjectId_SymbolKeyOnlyDescriptor_ResolvesWithoutReflection`
   - File: `src/Strategos.Ontology.Tests/Identity/ObjectIdentityProjectorTests.cs`
   - Build a descriptor with `ClrType = null, SymbolKey = "py::Foo"` and a source-supplied accessor over a dictionary-shaped instance; assert no reflection (accessor invoked, deterministic id).
2. [GREEN] Allow the id accessor to be supplied by `IOntologySource` for `SymbolKey`-only descriptors; projector resolves CLR accessor or source accessor, never `GetType().GetProperty(...)`.
   - Files: `src/Strategos.Ontology/Identity/ObjectIdentityProjector.cs`, `src/Strategos.Ontology/Sources/IOntologySource.cs`
3. [REFACTOR] Neither-accessor case → typed error ("descriptor has no id accessor"), never silent.

**Dependencies:** Task 2 · **Parallelizable:** No

### Task 4: Composite-key formatting + no `Type`-typed identity API
1. [RED] `ProjectId_CompositeKey_FormatsDeterministicallyWithSeparator`
   - File: `src/Strategos.Ontology.Tests/Identity/ObjectIdentityProjectorTests.cs`
2. [GREEN] Deterministic composite formatting (reserved separator); single-key path unchanged.
3. [REFACTOR] Assert (compile-time review) no public member in `Identity/` accepts/returns `System.Type` (INV-8).

**Dependencies:** Task 2 · **Parallelizable:** with Task 3

---

## Group 2 — DR-2 + DR-8(relate): in-memory relate-store

**Branch:** `feat/edge-foundation-dr2-relate-store` (stacked on DR-1)

### Task 5: `RelateAsync` / `UnrelateAsync` create and remove a row
1. [RED] `RelateAsync_TwoInstances_CreatesRow`, `UnrelateAsync_ExistingRow_RemovesIt`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryRelateStoreTests.cs` (new)
   - Expected failure: methods do not exist on `IObjectSetWriter`.
2. [GREEN] Add `RelateAsync(string srcDescriptor, string srcId, string linkName, string tgtDescriptor, string tgtId, CancellationToken ct = default)` and `UnrelateAsync(...)` to `IObjectSetWriter`; implement in `InMemoryObjectSetProvider` with `_relations: ConcurrentDictionary<(string,string,string), List<(string tgtDescriptor,string tgtId,string? assocId)>>`.
   - Files: `src/Strategos.Ontology/ObjectSets/IObjectSetWriter.cs`, `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`
3. [REFACTOR] Row tuple → a small `sealed record RelationRow` for readability.

**Dependencies:** Task 2 · **Parallelizable:** No

### Task 6: Idempotency + unrelate no-op
1. [RED] `RelateAsync_DuplicateTriple_IsIdempotent`, `UnrelateAsync_MissingRow_IsNoOp`
   - File: `InMemoryRelateStoreTests.cs`
2. [GREEN] Guard duplicate `(src, link, tgt)` insert; `UnrelateAsync` of absent row returns without throwing.
3. [REFACTOR] —

**Dependencies:** Task 5 · **Parallelizable:** No

### Task 7: Deterministic ordinal-by-id read order (INV-7 / replay)
1. [RED] `Relations_ReadOrder_IsOrdinalByTargetId`
   - File: `InMemoryRelateStoreTests.cs` — insert out of order, assert sorted read.
2. [GREEN] Read path returns rows ordered by `tgtId` (stable); `ConcurrentDictionary` enumeration never exposed raw.
3. [REFACTOR] —

**Dependencies:** Task 5 · **Parallelizable:** with Task 6

### Task 8 (DR-8): Eager endpoint validation → typed error
1. [RED] `RelateAsync_NonExistentEndpoint_ThrowsTypedError`
   - File: `InMemoryRelateStoreTests.cs` — relate to an unstored `tgtId`; assert a typed exception, no dangling row.
2. [GREEN] Validate both endpoint ids exist in `_items` before writing; throw a typed `RelationEndpointNotFoundException` naming the missing endpoint (DIM-2). Document this as the eager posture Npgsql FKs mirror.
3. [REFACTOR] —

**Dependencies:** Task 5 · **Parallelizable:** No

### Task 9 (DR-8): Self-loop policy
1. [RED] `RelateAsync_SelfLoop_WhenDisallowed_ThrowsTypedError`, `RelateAsync_SelfLoop_WhenAllowed_CreatesRow`
   - File: `InMemoryRelateStoreTests.cs`
2. [GREEN] Link-level `AllowsSelfLoop` flag (default false); self-loop `(x, link, x)` throws typed error unless opted in. (Analyzer promotion is DR-6, deferred.)
   - Files: `src/Strategos.Ontology/Descriptors/LinkDescriptor.cs`, `InMemoryObjectSetProvider.cs`
3. [REFACTOR] —

**Dependencies:** Task 8 · **Parallelizable:** No

---

## Group 3 — DR-3 + DR-8(traverse): instance-anchored traversal

**Branch:** `feat/edge-foundation-dr3-traversal` (stacked on DR-2)

### Task 10: Traverse resolves by source instance, not target type
1. [RED] `TraverseLink_FromInstance_ReturnsOnlyRelatedTargets`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTraversalTests.cs`
   - Relate `x→{a,b}`, store unrelated `c`; assert traverse from `x` yields `{a,b}` only.
2. [GREEN] Rewrite `EvaluateTraverseLink`: materialize upstream instances, project ids (DR-1 projector), look up `_relations` by `(srcId, linkName)`, resolve targets by id. Remove the `itemResolver(link.TargetTypeName)` call at line 139.
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs`
3. [REFACTOR] Extract target-by-id resolution helper.

**Dependencies:** Task 5, Task 2 · **Parallelizable:** No

### Task 11 (DR-8): Zero-relations empty-set regression guard for #114
1. [RED] `TraverseLink_InstanceWithNoRelations_ReturnsEmptySet`
   - File: `ObjectSetTraversalTests.cs` — the explicit #114 guard: an instance with no rows must NOT return all target-type items.
2. [GREEN] Covered by Task 10's rewrite; this test pins the regression.
3. [REFACTOR] —

**Dependencies:** Task 10 · **Parallelizable:** with Task 12

### Task 12: Association-backed traversal exposes edge attributes before far hop
1. [RED] `TraverseLink_OverAssociation_ExposesEdgeAttributesForFilter`
   - File: `ObjectSetTraversalTests.cs` — relate via an association carrying `Status`; assert `.Where(a => a.Status == Active)` filters before the far-endpoint hop.
2. [GREEN] When a row carries `assocId`, the traversal step yields the association object (filterable); a further hop resolves the far endpoint via the row's other side.
   - File: `InMemoryExpressionEvaluator.cs`
3. [REFACTOR] —

**Dependencies:** Task 10, Task 17 · **Parallelizable:** No

### Task 13 (DR-8): `SymbolKey`-only relate→traverse, no reflection
1. [RED] `RelateThenTraverse_SymbolKeyOnlyDescriptor_NoReflection`
   - File: `ObjectSetTraversalTests.cs` — full path on a CLR-free descriptor; INV-8 regression.
2. [GREEN] Covered by DR-1 + DR-3; this test pins the polyglot path end-to-end.
3. [REFACTOR] —

**Dependencies:** Task 10, Task 3 · **Parallelizable:** No

---

## Group 4 — DR-4: `Association<T>` + `ObjectKind.Association`

**Branch:** `feat/edge-foundation-dr4-association` (stacked on DR-1; attributed-relate seam needs DR-2)

### Task 14: `ObjectKind.Association`
1. [RED] `ObjectKind_DefinesAssociationMember`
   - File: `src/Strategos.Ontology.Tests/Builder/ObjectTypeBuilderTests.cs`
2. [GREEN] Add `Association` to `ObjectKind` (additive enum — backward compatible, DIM-3).
   - File: `src/Strategos.Ontology/Descriptors/ObjectKind.cs`
3. [REFACTOR] —

**Dependencies:** None · **Parallelizable:** with Group 1

### Task 15: `ontology.Association<TRel>(name, cfg)` authoring with typed endpoints
1. [RED] `Association_DeclaresRelationWithTwoTypedEndpoints`, `Association_WithProperty_CapturesEdgeAttribute`
   - File: `src/Strategos.Ontology.Tests/Builder/AssociationBuilderTests.cs` (new)
2. [GREEN] `IAssociationBuilder` / `sealed AssociationBuilder` with `Between<L>(sel).And<R>(sel)`, `Property(...)`; `Association<TRel>(string name, Action<IAssociationBuilder> cfg)` on `IOntologyBuilder` / `OntologyBuilder` producing an `ObjectTypeDescriptor` with `Kind = Association` + two endpoint refs.
   - Files: `src/Strategos.Ontology/Builder/IAssociationBuilder.cs`, `AssociationBuilder.cs`, `IOntologyBuilder.cs`, `OntologyBuilder.cs`; descriptor endpoint fields in `ObjectTypeDescriptor.cs`
3. [REFACTOR] All new builder/descriptor types `sealed` (INV-6); association object immutable record (INV-7); endpoints typed via descriptors, not `typeof` (INV-8).

**Dependencies:** Task 14 · **Parallelizable:** with Group 2/3 authoring

### Task 16: Graph builder recognizes associations and exposes endpoints as edges
1. [RED] `OntologyGraph_AssociationObject_ExposesEndpointsAsEdge`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphBuilderTests.cs` (exists — extend)
2. [GREEN] `OntologyGraphBuilder` treats `Kind == Association` objects as edges whose source/destination are the two endpoints.
   - File: `src/Strategos.Ontology/OntologyGraphBuilder.cs`
3. [REFACTOR] —

**Dependencies:** Task 15 · **Parallelizable:** No

### Task 17: Attributed-relate seam (store association object + row with `assocId`)
1. [RED] `RelateWithAssociation_StoresObjectAndRowReferencingIt`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryRelateStoreTests.cs`
2. [GREEN] Overload/path that `StoreAsync`-es the association object then writes a row whose `assocId` is the association object's projected id.
   - Files: `IObjectSetWriter.cs`, `InMemoryObjectSetProvider.cs`
3. [REFACTOR] —

**Dependencies:** Task 5, Task 15 · **Parallelizable:** No

---

## Group 5 — Invariant guards (mechanical enforcement, not review-by-convention)

**Branch:** folds into the DR-1 / DR-4 branches that introduce the surfaces being guarded.

> Added in plan-review: the design asserts INV-2 / INV-6 / INV-8 conformance "by construction." That is a hand-wavy mitigation. Each is converted here into a failing-test-first mechanical guard. No new test package required — plain reflection over the loaded assembly.

### Task 18 (INV-2): assembly-reference self-containment guard
1. [RED] `Ontology_Assembly_ReferencesNoWolverineOrMarten`
   - File: `src/Strategos.Ontology.Tests/InvariantGuardTests.cs` (new)
   - Assert `typeof(ObjectTypeDescriptor).Assembly.GetReferencedAssemblies()` contains no name starting with `Wolverine` or `Marten`. Fails the moment the relate-store or projector pulls in an event-store dependency.
2. [GREEN] Passes at baseline; the test is the standing regression guard for the DR-2 relate-store work.
3. [REFACTOR] —

**Dependencies:** Task 5 (lands with the relate-store, the most likely place to violate) · **Parallelizable:** No

### Task 19 (INV-6 + INV-8): sealed + no `Type`-typed identity, as tests
1. [RED] `NewEdgeTypes_AreSealed`, `IdentityApi_ExposesNoSystemTypeMembers`
   - File: `src/Strategos.Ontology.Tests/InvariantGuardTests.cs`
   - Reflect over the new `Identity/` + association types: assert each is `sealed`; assert no public member in the `Identity` namespace has a `System.Type` parameter or return (INV-8 "typed on identity, not `Type`").
2. [GREEN] Covered by the DR-1 / DR-4 type shapes; this pins the structural invariants so a later edit can't silently unseal or reintroduce a `Type`-typed identity API.
3. [REFACTOR] —

**Dependencies:** Task 4, Task 15 · **Parallelizable:** with Task 18

---

## Parallelization summary

- **Sequential spine:** Task 1 → 2 → 5 → 10. (identity accessor → projector → relate-store → traversal)
- **Parallel-safe within a branch:** {Task 3, Task 4}; {Task 6, Task 7}; {Task 11, Task 13}.
- **Group 4 (DR-4)** can begin from Task 14 in parallel with Group 1 once `ObjectKind` lands; Task 17 joins after both Task 5 and Task 15; Task 12 (assoc traversal) is the last join (needs Task 10 + Task 17).
- **Stacked branches:** DR-2 stacks on DR-1; DR-3 stacks on DR-2; DR-4 stacks on DR-1. Rebase DR-3/DR-4 onto merged main as parents land (per the stacked-branch gate discipline).

## Coverage map (design DR → tasks)

| Design requirement | Tasks |
|---|---|
| DR-1 object-identity contract | 1, 2, 3, 4 |
| DR-2 relate-store + relate/unrelate | 5, 6, 7 |
| DR-3 instance-anchored traversal | 10, 12 |
| DR-4 `Association<T>` + `ObjectKind.Association` | 14, 15, 16, 17 |
| DR-8 (in-memory slice) failure modes | 8 (dangling), 9 (self-loop), 11 (zero-relations #114 guard), 13 (polyglot no-reflection) |
| INV-2 self-containment (mechanical) | 18 |
| INV-6 sealed + INV-8 no-`Type` identity (mechanical) | 19 |
| INV-7 immutable / replay-deterministic | 7, 15 |
