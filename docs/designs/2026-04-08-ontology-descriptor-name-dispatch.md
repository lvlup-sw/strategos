# Strategos 2.4.1 — Ontology Descriptor-Name Dispatch

**Date:** 2026-04-08
**Issues:** #31 (descriptor name discarded in `GetObjectSet<T>`), #32 (future Option Y follow-up)
**Closes:** #28 and #29 (delivered in 2.4.0 via #30 — housekeeping)
**Target package version:** LevelUp.Strategos.Ontology 2.4.1, LevelUp.Strategos.Ontology.Npgsql 2.4.1
**Downstream consumer:** Basileus `OntologyContextAssembler` (per-collection `SemanticDocument` isolation)
**Status:** Proposed (Approach 1 + Option X — name threads through expression tree, multi-registration is leaf-only)

---

## 1. Problem Statement

Strategos 2.4.0 shipped `IOntologyQuery.GetObjectSet<T>(string objectType)` as the typed entry point to the fluent similarity chain. The canonical Basileus call site in the 2.4.0 design (`docs/designs/2026-04-06-ontology-2-4-0.md` §3.2) iterates over a list of `RagCollection` entries with distinct `ObjectType` strings, expecting each to dispatch to its own physical pgvector table:

```csharp
foreach (var collection in profile.RagCollections)
{
    var hits = await _ontologyQuery
        .GetObjectSet<SemanticDocument>(collection.ObjectType)  // "trading_documents" | "knowledge_documents"
        .SimilarTo(query)
        .ExecuteAsync(cancellationToken);
    // ...
}
```

**The implementation silently discards the descriptor name.** `OntologyQueryService.GetObjectSet<T>` uses the `objectType` parameter to throw `KeyNotFoundException` on unknown names, then constructs `new ObjectSet<T>(...)` without threading the name into the expression tree (`OntologyQueryService.cs:49-68`). `ObjectSet<T>`'s root is `new RootExpression(typeof(T))` — the descriptor name never reaches the provider. `PgVectorObjectSetProvider` dispatches purely by `TypeMapper.GetTableName<T>()`, which is `ToSnakeCase(typeof(T).Name)`. Two calls with different `objectType` strings but the same `T` execute against the same physical table.

**Downstream consequence.** Basileus cannot physically isolate per-collection semantic documents as the 2.4.0 design intended. It currently works around this by registering `SemanticDocument` exactly once via a domain-agnostic `BasileusDataFabricOntology`, accepting that all collections share one table and using `SourceDomain` as the only discriminator. This defeats the per-collection storage isolation that 2.4.0 was designed to enable, and the fallback is tracked on the Basileus side in commit `8a4a9490`.

**Scope of the fix.** The 2.4.0 design intent is correct — descriptor name is meant to be the dispatch key. The implementation just failed to thread it through. This fix makes the implementation match the design, with **no new architectural concepts introduced**. Multi-registration of a CLR type under multiple descriptor names becomes the mechanism that makes the parameter meaningful; the fix is a refinement of 2.4.0, not a new feature.

---

## 2. Design Intent — What the Spec and 2.4.0 Design Already Say

**The descriptor name is already a first-class field on `ObjectTypeDescriptor`.** `ObjectTypeBuilder.Build()` constructs `new(typeof(T).Name, typeof(T), domainName)` (`ObjectTypeBuilder.cs:136`) — the name is stored, but it cannot be overridden and does not flow through to dispatch. The schema already supports what we need; only the wiring is missing.

**The 2.4.0 "single source of truth" principle applies directly.** 2.4.0 §2 articulates this as the reason for rejecting `ActionDescriptor.ValidFromStates`: the spec recognizes exactly one set of identifiers and refuses to introduce a parallel one. The same principle applies here — descriptor name and CLR type are currently two parallel dispatch keys (builder uses name, provider uses type), and the two have silently drifted. The fix consolidates dispatch through descriptor name end-to-end, making CLR type a compile-time constraint and descriptor name the runtime routing key.

**The 2.4.0 "spec-aligned sugar over schema change" discipline applies.** 2.4.0's Track A used build-time projection to satisfy a new user-facing API without modifying descriptor schema. This fix uses the same pattern: the `Object<T>(string? name, config)` overload is pure builder sugar; the descriptor already has a `Name` field; the expression tree gains one additive field (`RootExpression.ObjectTypeName`) and a walk-to-root helper for derived nodes. No schema migrations, no interface-shape changes to `IObjectSetProvider`, no parallel generic `IObjectSetProvider<T>`.

**The 2.4.0 "What Is Not Changing" table needs honest revision.** Three entries are affected, all minimally:

| Surface | 2.4.0 status | 2.4.1 delta |
|---|---|---|
| `IObjectSetProvider` interface shape | Unchanged | **Still unchanged** — dispatch key travels in the expression, not as a parameter |
| `SimilarityExpression` shape | Unchanged | **Still unchanged** — `RootObjectTypeName` is a computed property that walks to root |
| `RootExpression` shape | (not listed) | **Additive:** one required constructor parameter `ObjectTypeName: string` |
| pgvector schema | Unchanged | **Keying convention clarified:** tables are per descriptor name. For single-registration (the default), `typeof(T).Name` is the descriptor name, so physical tables are unchanged. No migration |
| `ObjectTypeBuilder` / `IOntologyBuilder.Object<T>` | (not listed) | **Additive overload:** `Object<T>(string? name, config)` |
| Graph key | (implicit `(domain, CLR type)`) | **Changed to `(domain, name)`.** For single-registration, equivalent to old behavior. Promotes today's implicit `ToDictionary` failure on duplicate names into an explicit `AONT040` diagnostic |

---

## 3. Approach 1 — Name Threads Through the Expression Tree

The fix splits into six concrete changes, each scoped to a small set of files. All changes are additive except where noted.

### 3.1 Builder: `Object<T>(string? name, config)` overload

`IOntologyBuilder` and `OntologyBuilder` gain a second `Object<T>` overload accepting an explicit descriptor name. The existing `Object<T>(config)` overload remains and is equivalent to `Object<T>(name: null, config)`. `ObjectTypeBuilder<T>` takes an optional `explicitName` in its constructor and uses it in `Build()` instead of `typeof(T).Name` when non-null:

```csharp
public void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure) where T : class
{
    var builder = new ObjectTypeBuilder<T>(domainName, explicitName: name);
    configure(builder);
    _objectTypes.Add(builder.Build());
}

// Build() change:
var descriptorName = _explicitName ?? typeof(T).Name;
return new(descriptorName, typeof(T), domainName) { /* ... */ };
```

**Name validation.** Explicit names are validated at construction time against the regex `^[a-zA-Z_][a-zA-Z0-9_]*$` (same rule as C# identifiers, which ensures the name is safe for snake-case table conversion and safe as a dictionary key). Invalid names throw `ArgumentException` with a clear message. The default-name case is unchanged and validation is skipped (since `typeof(T).Name` is by definition a valid C# identifier).

**Back-compat.** Existing `Object<T>(config)` callers are unaffected — same default name, same table, same dispatch.

### 3.2 Graph freeze: rekey by `(domain, name)` and add reverse index

`OntologyGraphBuilder.Build()` already keys its lookup by descriptor name within domain (`g.ToDictionary(ot => ot.Name)` at line 61), but this lookup fails with a cryptic `ArgumentException` if two descriptors in the same domain share a name. The fix promotes this failure into an explicit diagnostic:

**`AONT040 DuplicateObjectTypeName`** — fires when two `ObjectTypeDescriptor` entries in the same domain have the same `Name`. The error message names both CLR types (so users can tell whether they accidentally registered the same name twice, or registered the same type twice without explicit names):

> Object type name 'trading_documents' is registered twice in domain 'Basileus'. First registration: CLR type `SemanticDocument`. Second registration: CLR type `SemanticDocument`. Either remove one registration, or specify distinct names via `Object<T>("name", ...)`.

**`AONT041 MultiRegisteredTypeInLink`** — fires when a CLR type has multiple registrations in any domain *and* is referenced as a link source or target in any domain. The check walks every link descriptor's `TargetType` and `SourceType` against the reverse index. Error message:

> CLR type `SemanticDocument` has multiple registrations (`trading_documents`, `knowledge_documents`) but is also referenced as a link target in 'Portfolio.Documents'. Multi-registered types cannot participate in structural links. See #32 for a future relaxation path.

**Reverse index.** Graph freeze builds a `Dictionary<Type, IReadOnlyList<string>>` mapping each CLR type to its list of descriptor names across all domains. This is a free byproduct of the existing object type iteration and is stored on `OntologyGraph` as a new public property `ObjectTypeNamesByType`. It powers the `AONT041` check and the public `IOntologyQuery.GetObjectTypeNames<T>()` method (§3.5).

### 3.3 Expression tree: `RootExpression.ObjectTypeName` + walk-to-root helper

`RootExpression` gains a required `ObjectTypeName: string` constructor parameter. Its constructor stores the name alongside the existing `ObjectType: Type`. `ObjectSetExpression` (the base class) gains a protected helper method `GetRootObjectTypeName()` that walks `Source` references until it reaches a `RootExpression` and returns its `ObjectTypeName`:

```csharp
public abstract class ObjectSetExpression
{
    // existing: ObjectType: Type

    /// <summary>
    /// Walks to the root of this expression tree and returns the descriptor
    /// name of the RootExpression. Used by providers to resolve dispatch.
    /// </summary>
    public string RootObjectTypeName => WalkToRoot(this).ObjectTypeName;

    private static RootExpression WalkToRoot(ObjectSetExpression expr) => expr switch
    {
        RootExpression root => root,
        FilterExpression f => WalkToRoot(f.Source),
        TraverseLinkExpression t => WalkToRoot(t.Source),  // constrained: target is single-registration under Option X
        InterfaceNarrowExpression i => WalkToRoot(i.Source),
        RawFilterExpression r => WalkToRoot(r.Source),
        IncludeExpression i => WalkToRoot(i.Source),
        SimilarityExpression s => WalkToRoot(s.Source),
        _ => throw new InvalidOperationException($"Unknown expression type: {expr.GetType().Name}")
    };
}
```

**Why a tree-walk instead of a copy-propagated field.** Copy-propagation forces every expression constructor to accept, validate, and store the name — a ~5x larger refactor surface with no semantic benefit for the single-root case we're targeting. The walk is O(depth), and expression depth is small and bounded in practice (typically 1-3 nodes). If profiling ever shows this as a hotspot, caching the root reference on each derived expression is a trivial follow-up.

**`TraverseLinkExpression` and link traversal.** Under Option X (§1), multi-registered types cannot be link targets (enforced by `AONT041`). Therefore every link target has exactly one descriptor registration, and `WalkToRoot` on a traverse expression correctly returns the **source** descriptor's name — but after traversal, the expression produces objects of the target type. This is the one subtlety: `TraverseLinkExpression.ObjectType` is the linked type, but `RootObjectTypeName` returns the source's root name, which is wrong after traversal.

**Resolution.** `TraverseLinkExpression` overrides the walk: its `RootObjectTypeName` returns `typeof(TLinked).Name` directly, which is unambiguous under Option X because link targets are always single-registration. `WalkToRoot` terminates at `TraverseLinkExpression` rather than recursing into its source. This preserves the dispatch guarantee: after a traverse, the expression carries the target's (only) descriptor name.

### 3.4 Query service + ObjectSet: thread the name through

`OntologyQueryService.GetObjectSet<T>` reads the resolved `ObjectTypeDescriptor`'s `Name` field and passes it into a new `ObjectSet<T>` constructor parameter. `ObjectSet<T>` passes the name into `new RootExpression(typeof(T), descriptorName)`:

```csharp
public ObjectSet<T> GetObjectSet<T>(string objectType) where T : class
{
    var ot = FindObjectType(objectType)
        ?? throw new KeyNotFoundException($"Object type '{objectType}' is not registered in the ontology.");

    return new ObjectSet<T>(
        descriptorName: ot.Name,   // NEW
        _objectSetProvider!,
        _actionDispatcher!,
        _eventStreamProvider!);
}
```

The read-only `OntologyQueryService(OntologyGraph)` constructor continues to work for callers that never invoke `GetObjectSet<T>`; its existing `InvalidOperationException` branch remains unchanged.

### 3.5 Provider dispatch: read from expression, remove `GetTableName<T>()`

`PgVectorObjectSetProvider.ExecuteSimilarityAsync<T>`, `ExecuteAsync<T>`, and `StreamAsync<T>` read `expression.RootObjectTypeName` and call a new pure-function helper `TypeMapper.ToSnakeCase(string name)` to produce the table name. The old `TypeMapper.GetTableName<T>()` method is **removed** entirely — there is no valid caller after this change, and leaving it in place would preserve the silent-wrong-dispatch footgun the fix is eliminating. Removal is a source-level break, but only to internal code (the method is `internal`).

`InMemoryObjectSetProvider` switches its storage partition key from `ConcurrentDictionary<Type, List<object>>` to `ConcurrentDictionary<string, List<object>>`, keyed by descriptor name. The `Seed<T>` method gains an optional `descriptorName` parameter that defaults to `typeof(T).Name` (so existing tests continue to pass without modification). All query methods read the partition key from `expression.RootObjectTypeName`. This ensures test fixtures observe the same multi-registration semantics as production.

### 3.6 Public reverse-index API on `IOntologyQuery`

`IOntologyQuery` gains one new method:

```csharp
/// <summary>
/// Returns all descriptor names registered for the given CLR type across the composed ontology.
/// Empty list if <typeparamref name="T"/> is not registered. Used by consumers (e.g. Basileus)
/// that need to enumerate per-collection partitions of a shared content-carrier type without
/// hardcoding descriptor names at call sites.
/// </summary>
IReadOnlyList<string> GetObjectTypeNames<T>() where T : class;
```

Implementation reads directly from `OntologyGraph.ObjectTypeNamesByType`. Returns an empty list (not a throw) for unregistered types, consistent with `GetObjectTypes(domain, interface, includeSubtypes)` semantics. Basileus will use this to iterate over configured collections without a hardcoded name list in its `RagCollections` configuration code.

### 3.7 Write-path dispatch: `IObjectSetWriter` descriptor-name overloads

The read path carries the descriptor name in the expression tree, but `IObjectSetWriter.StoreAsync<T>(T item, ...)` and `StoreBatchAsync<T>(IReadOnlyList<T> items, ...)` take no expression — they receive an item and a CLR type only. `PgVectorObjectSetProvider`'s write-path call sites (`StoreAsync`, `StoreBatchAsync`, `EnsureSchemaAsync`) currently all use `TypeMapper.GetTableName<T>()`. If multi-registration is to work symmetrically across reads and writes, the writer must know which descriptor the caller is targeting.

**Design choice: explicit-name overloads on `IObjectSetWriter` with a single-registration default.** The interface gains descriptor-name overloads alongside the existing signatures:

```csharp
public interface IObjectSetWriter
{
    // Existing — resolves descriptor name from the graph: if T has exactly one registration,
    // uses that name; if T has multiple registrations, throws InvalidOperationException
    // with a message instructing the caller to use the explicit-name overload.
    Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class;
    Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class;

    // NEW — explicit descriptor name. Always unambiguous.
    Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class;
    Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class;
}
```

**Why two overloads instead of one.** The single-registration case is the overwhelming majority of calls in existing and future code; requiring every call site to thread an explicit name would be ergonomic regression for 99% of usage. The default overload resolves via the new `OntologyGraph.ObjectTypeNamesByType` reverse index: exactly-one-name → use it; zero or multiple → throw with a clear diagnostic pointing at the explicit-name overload. This makes the default overload safe (it cannot silently dispatch to the wrong table) and keeps single-registration callers untouched.

**How the resolver reaches the graph.** `PgVectorObjectSetProvider`'s constructor is updated to take an optional `OntologyGraph?` parameter (registered by the DI pipeline at the same time as the graph itself). When the graph is present, the default `StoreAsync<T>(T)` overload calls `graph.ObjectTypeNamesByType.GetValueOrDefault(typeof(T))` and applies the exactly-one rule. When the graph is absent (e.g., direct instantiation in a unit test), the default overload falls back to `typeof(T).Name` for backwards compatibility with existing tests — the fallback is documented but not recommended for production.

**`InMemoryObjectSetProvider`** implements the same pattern: the default `StoreAsync<T>(T)` overload uses `typeof(T).Name` as the descriptor name (preserving all existing test code), while the new explicit-name overload partitions by the supplied name. Tests that exercise multi-registration use the explicit overload.

**`EnsureSchemaAsync<T>` gets the same treatment.** `EnsureSchemaAsync<T>()` becomes `EnsureSchemaAsync<T>(string? descriptorName = null)` — name parameter optional, resolved via the same graph path. Schema creation for multi-registered types requires calling `EnsureSchemaAsync<T>("trading_documents")` once per descriptor.

**`IngestionPipeline<T>.WriteTo()` plumbing.** The ingestion pipeline builder's `WriteTo(IObjectSetWriter writer)` method gains a sibling `WriteTo(IObjectSetWriter writer, string descriptorName)`. When the descriptor name is supplied, `IngestionPipeline<T>.ExecuteCoreAsync` calls `_writer.StoreBatchAsync<T>(descriptorName, mappedItems, ct)`; otherwise it calls the existing default overload. This is the path Basileus will use to ingest per-collection `SemanticDocument` batches once multi-registration lands.

---

## 4. What Is *Not* Changing

Normative list — reviewers should reject any task that touches an item below.

| Surface | Status | Reason |
|---|---|---|
| `ActionDescriptor` schema | Unchanged | Unrelated to dispatch |
| `LifecycleDescriptor` schema | Unchanged | Unrelated to dispatch |
| `ObjectTypeDescriptor` schema | Unchanged | `Name` field already exists; only its provenance changes (explicit vs default) |
| `IObjectSetProvider` interface shape | Unchanged | Dispatch key travels inside the expression tree, not as a parameter |
| `IObjectSetWriter` interface shape | **Additive** | Two new descriptor-name overloads (`StoreAsync`, `StoreBatchAsync`); existing signatures unchanged and default to single-registration resolution |
| `SimilarityExpression` shape | Unchanged | `RootObjectTypeName` is a computed walk, not a stored field |
| `FilterExpression` / `TraverseLinkExpression` / `InterfaceNarrowExpression` / `RawFilterExpression` / `IncludeExpression` shapes | Unchanged (except `TraverseLinkExpression` override) | Walk-to-root handles all of them uniformly |
| pgvector physical schema | Unchanged | Per-descriptor-name tables; for single-registration (the default), identical to 2.4.0 |
| Link DSL (`HasMany`/`HasOne`/`ManyToMany`/`RequiresLink`/`CreatesLinked`) | Unchanged | Multi-registration is leaf-only under Option X (#32) |
| `ObjectSet<T>.Where` / `.TraverseLink` / `.OfInterface` / `.Include` / `.SimilarTo` | Unchanged | Expression tree walks handle the name propagation |
| `SimilarObjectSet<T>` fluent setters (`WithMinRelevance`/`Take`/`WithMetric`) | Unchanged | Added in 2.4.0 Track B; untouched here |
| `.ValidFromState` lifecycle sugar (2.4.0 Track A) | Unchanged | Orthogonal to dispatch |
| Existing Basileus workaround (`BasileusDataFabricOntology` single-registration) | Migrated post-release | Basileus will split `SemanticDocument` into per-collection registrations after 2.4.1 ships; tracked separately |
| Generator-emitted code (source generators) | Unchanged | Generator emits builder calls using `Object<T>(config)`; users opt into the explicit-name overload manually |

---

## 5. Test Plan

All tests use TUnit with NSubstitute (existing convention). Descriptor-name dispatch is tested at the builder, graph, expression, query, and provider layers. TUnit invocation: `dotnet test -- --treenode-filter "/*/*/*/TestName"` per the established convention.

### 5.1 Builder-layer tests (`Strategos.Ontology.Tests/Builder/`)

- `ObjectTypeBuilder_with_explicit_name_uses_explicit_name` — `Object<T>("custom_name", ...)` produces a descriptor with `Name == "custom_name"`
- `ObjectTypeBuilder_without_explicit_name_uses_typeof_T_name` — regression guard for default behavior
- `ObjectTypeBuilder_with_explicit_name_validates_identifier_regex` — `Object<T>("has spaces", ...)` throws `ArgumentException`
- `ObjectTypeBuilder_with_null_explicit_name_falls_back_to_default` — `Object<T>(null, ...)` is equivalent to `Object<T>(...)`

### 5.2 Graph-freeze tests (`Strategos.Ontology.Tests/OntologyGraphBuilderTests.cs`)

- `GraphBuilder_allows_same_clr_type_under_distinct_names_in_same_domain` — multi-registration succeeds when names differ
- `GraphBuilder_raises_AONT040_when_same_descriptor_name_registered_twice` — explicit diagnostic replaces today's implicit `ArgumentException`
- `GraphBuilder_allows_same_descriptor_name_across_different_domains` — `(domain, name)` is the key, not just `name`
- `GraphBuilder_raises_AONT041_when_multi_registered_type_is_link_target` — Option X enforcement (source side)
- `GraphBuilder_raises_AONT041_when_multi_registered_type_is_link_source` — Option X enforcement (target side)
- `GraphBuilder_allows_multi_registered_leaf_type_without_links` — positive case: `SemanticDocument` registered twice with no structural link references succeeds
- `GraphBuilder_exposes_ObjectTypeNamesByType_reverse_index` — single-registration and multi-registration both populate correctly

### 5.3 Expression-tree tests (`Strategos.Ontology.Tests/ObjectSets/ObjectSetExpressionTests.cs`)

- `RootExpression_requires_ObjectTypeName_in_constructor` — cannot construct without descriptor name
- `FilterExpression_RootObjectTypeName_walks_to_source_root` — walk-to-root correctness
- `SimilarityExpression_RootObjectTypeName_walks_to_source_root` — similarity case specifically (this is the one Basileus exercises)
- `InterfaceNarrowExpression_RootObjectTypeName_walks_to_source_root`
- `IncludeExpression_RootObjectTypeName_walks_to_source_root`
- `RawFilterExpression_RootObjectTypeName_walks_to_source_root`
- `TraverseLinkExpression_RootObjectTypeName_returns_linked_type_name` — override behavior; guarded by Option X single-registration invariant
- `ComposedExpression_Root_Filter_Similarity_returns_correct_name` — end-to-end: `GetObjectSet("trading").Where(...).SimilarTo("q")` walks to `"trading"`

### 5.4 Query-service tests (`Strategos.Ontology.Tests/Query/OntologyQueryServiceTests.cs`)

- `GetObjectSet_threads_descriptor_name_into_RootExpression` — the core dispatch-correctness test; the regression guard against #31 reappearing
- `GetObjectSet_unknown_name_throws_KeyNotFoundException` — existing behavior preserved
- `GetObjectSet_multi_registration_returns_distinct_root_expressions_for_each_name` — the multi-registration happy path
- `GetObjectTypeNames_returns_all_registrations_for_multi_registered_type`
- `GetObjectTypeNames_returns_single_entry_for_single_registered_type`
- `GetObjectTypeNames_returns_empty_list_for_unregistered_type`

### 5.5 InMemory provider tests (`Strategos.Ontology.Tests/ObjectSets/InMemoryObjectSetProviderTests.cs`)

- `InMemoryProvider_partitions_by_descriptor_name_not_clr_type` — seed `SemanticDocument` under name "trading_documents"; query under name "knowledge_documents"; assert empty result
- `InMemoryProvider_default_seed_uses_typeof_T_name` — regression guard for existing test code
- `InMemoryProvider_similarity_search_dispatches_by_descriptor_name` — end-to-end: seed under two distinct names, run `SimilarTo` on each, verify distinct result sets

### 5.6 PgVector provider tests — reads (`Strategos.Ontology.Npgsql.Tests/`)

- `PgVectorProvider_ExecuteSimilarityAsync_uses_descriptor_name_from_expression` — SQL generation asserts the `FROM` clause is `trading_documents`, not `semantic_document`
- `PgVectorProvider_ExecuteAsync_uses_descriptor_name_from_expression` — non-similarity path
- `PgVectorProvider_StreamAsync_uses_descriptor_name_from_expression`
- `PgVectorProvider_default_name_unchanged` — single-registration regression guard: `Object<SemanticDocument>(...)` still hits table `semantic_document`
- `PgVectorProvider_TypeMapper_GetTableName_of_T_is_removed` — compile-time guard that the footgun is gone (a simple reflection test asserting the method no longer exists)

### 5.7 Write-path tests — `IObjectSetWriter` descriptor-name overloads

**`Strategos.Ontology.Tests/ObjectSets/IObjectSetWriterTests.cs` (new file):**

- `InMemoryWriter_StoreAsync_default_overload_uses_typeof_T_name` — backwards-compat regression guard
- `InMemoryWriter_StoreAsync_explicit_name_overload_uses_supplied_name` — new overload threads through correctly
- `InMemoryWriter_StoreBatchAsync_explicit_name_overload_partitions_by_name` — batch variant
- `InMemoryWriter_StoreAsync_default_overload_throws_when_type_has_multiple_registrations` — safety check: default overload refuses to silently guess for multi-registered types (when graph is present)
- `InMemoryWriter_StoreAsync_default_overload_falls_back_to_typeof_T_name_when_graph_absent` — unit-test-mode fallback

**`Strategos.Ontology.Npgsql.Tests/PgVectorWriteTests.cs`:**

- `PgVectorProvider_StoreAsync_explicit_name_writes_to_named_table` — SQL generation asserts `INSERT INTO trading_documents`
- `PgVectorProvider_StoreBatchAsync_explicit_name_uses_COPY_to_named_table` — COPY target table assertion
- `PgVectorProvider_EnsureSchemaAsync_with_explicit_name_creates_named_table` — DDL target table assertion
- `PgVectorProvider_StoreAsync_default_overload_resolves_via_graph_reverse_index` — graph-backed single-registration resolution
- `PgVectorProvider_StoreAsync_default_overload_throws_with_diagnostic_when_type_has_multiple_registrations` — safety check with clear error message

**`Strategos.Ontology.Tests/Ingestion/IngestionPipelineTests.cs`:**

- `IngestionPipeline_WriteTo_with_descriptor_name_threads_name_into_StoreBatchAsync` — pipeline builder overload plumbs the name through
- `IngestionPipeline_WriteTo_without_descriptor_name_calls_default_StoreBatchAsync` — backwards-compat for existing pipeline tests

### 5.8 Housekeeping

- Close issues #28 and #29 with a comment pointing to commit `9e47550` (delivered in 2.4.0 via #30). Both issues were resolved by the 2.4.0 ship; this design doc closes them as part of 2.4.1 scope cleanup.

---

## 6. Migration & Compatibility

**Strategos internal.** The `ObjectSet<T>(IObjectSetProvider, IActionDispatcher, IEventStreamProvider)` constructor signature changes — it gains a required `descriptorName: string` parameter. Only `OntologyQueryService.GetObjectSet<T>` constructs `ObjectSet<T>` directly today, so the surface is contained. Internal tests that construct `ObjectSet<T>` directly (there are a few in `ObjectSetTests.cs`) will be updated to pass an explicit name — the diff is small and mechanical. `TypeMapper.GetTableName<T>()` is removed; one internal provider call site is updated.

**Strategos published consumers.** None other than Basileus (unshipped). No NuGet migration required.

**Basileus integration (separate PR, post-2.4.1 NuGet bump).** Basileus will:
1. Split `SemanticDocument` out of `BasileusDataFabricOntology` and register it per-collection via `Object<SemanticDocument>("trading_documents", ...)` and `Object<SemanticDocument>("knowledge_documents", ...)` in the respective domain ontologies.
2. Remove the `SourceDomain` discriminator filter in `OntologyContextAssembler.SearchViaObjectSetsAsync` — per-collection isolation is now physical, not logical.
3. Optionally use the new `_ontologyQuery.GetObjectTypeNames<SemanticDocument>()` API to drive the `RagCollections` configuration loop without hardcoded name lists.

**No breaking changes for single-registration consumers.** Every existing `Object<T>(config)` call site continues to work with identical dispatch semantics. The 2.4.0 ObjectSet tests continue to pass with an explicit `descriptorName: typeof(T).Name` argument added in one place.

---

## 7. Open Questions

1. **Should `Object<T>(string? name, ...)` validate the name as snake_case specifically, or any C# identifier?** Leaning **any C# identifier** — consistent with table-name convention where the provider snake-cases at generation time. Plan phase decision.

2. **Should `IOntologyQuery.GetObjectTypeNames<T>()` return names sorted alphabetically or in registration order?** Leaning **registration order** — deterministic and aligned with how users typically think about their registrations. Plan phase decision.

3. **Error message quality for `AONT040`.** Should the message include the file/line of each duplicate registration? Probably out of scope — that requires source-info threading through the builder, which is a Roslyn-generator-level concern not a runtime one. Document as a post-2.4.1 enhancement if users request it.

4. **`.ValidFromState` + multi-registration interaction.** Does `.ValidFromState` on an action for a multi-registered type apply to all registrations? Yes, because `.ValidFromState` is a projection into the lifecycle (which is per-descriptor), and each registration has its own descriptor — so each registration gets its own lifecycle and its own projection. No special handling required. Plan phase should add one test to lock this in.

---

## 8. References

### Primary

- **Issue #31** — Bug report with the proposed 5-step fix (aligned with this design, minus the AONT041 link-participation rule)
- **Issue #32** — Follow-up tracking Option Y (multi-registered link participation) for future work
- **Design precedent:** `docs/designs/2026-04-06-ontology-2-4-0.md` §2 (single-source-of-truth principle), §3 (spec-aligned sugar discipline), §4 (what is not changing)
- **Spec:** `docs/reference/platform-architecture.md` §4.14.4 (Core Primitives), §4.14.11 (Domain Definition)
- **Basileus design:** Basileus 2.4.0 data fabric completion design — downstream consumer that drove the bug report

### Current implementation (call sites touched)

- `src/Strategos.Ontology/Builder/OntologyBuilder.cs:18` — `Object<T>` entry point
- `src/Strategos.Ontology/Builder/ObjectTypeBuilder.cs:136` — descriptor name construction
- `src/Strategos.Ontology/OntologyGraphBuilder.cs:59-61` — graph key and duplicate detection
- `src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs:24` — `RootExpression`
- `src/Strategos.Ontology/ObjectSets/ObjectSet.cs:22-33` — ObjectSet constructors
- `src/Strategos.Ontology/Query/OntologyQueryService.cs:49-68` — the discard site
- `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs:21` — partition key
- `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs:60` — dispatch site
- `src/Strategos.Ontology.Npgsql/Internal/TypeMapper.cs:53` — `GetTableName<T>()` removal

### Downstream (not modified in this PR)

- Basileus `OntologyContextAssembler.SearchViaObjectSetsAsync` — post-release migration target
- Basileus `BasileusDataFabricOntology` — post-release replacement with per-collection registrations
