# Implementation Plan: Strategos 2.4.1 — Ontology Descriptor-Name Dispatch

## Source Design
Link: `docs/designs/2026-04-08-ontology-descriptor-name-dispatch.md`

## Scope

**Target:** Approach 1 + Option X — name threads through the expression tree via walk-to-root; multi-registration is leaf-only (enforced by `AONT041`); writes get explicit-name overloads with graph-backed resolution for the default path.

**Target packages:** `LevelUp.Strategos.Ontology` 2.4.1, `LevelUp.Strategos.Ontology.Npgsql` 2.4.1 (version auto-resolved by MinVer at release time via the `v2.4.1` git tag).

**Excluded** (per design doc §4 — *What Is Not Changing*):
- Any modification to `ActionDescriptor` / `LifecycleDescriptor` / `ObjectTypeDescriptor` schemas
- Any modification to `IObjectSetProvider` interface shape
- Any modification to `SimilarityExpression` / `FilterExpression` / `InterfaceNarrowExpression` / `RawFilterExpression` / `IncludeExpression` shapes
- Any modification to link DSL (`HasMany`/`HasOne`/`ManyToMany`/`RequiresLink`/`CreatesLinked`) — multi-registration is leaf-only
- Any modification to `.ValidFromState` lifecycle sugar (2.4.0 Track A)
- Introduction of `IObjectSetProvider<T>` parallel generic interface
- Generator-emitted code (users opt into explicit-name overload manually)
- Basileus consumer-side migration (separate PR after NuGet bump)
- Option Y (multi-registered types participating in structural links) — tracked in #32

## Summary

- **Total tasks:** 28 (grouped into 7 tracks)
- **Parallel tracks:** 3 primary at Group 1 (A, B, F1 interface change); dependent tracks converge at Groups 2–5
- **Estimated test count:** 34 new tests across builder, graph freeze, expression tree, query service, read path, write path, and end-to-end layers
- **Design coverage:** 100% — every section in §3.1 through §3.7 and §5.1 through §5.8 has a task

## Spec Traceability

| Design Section | Task IDs | Status |
|---|---|---|
| §3.1 — `Object<T>(string? name, config)` builder overload | B1, B2, B3 | Covered |
| §3.2 — Graph rekey + `AONT040` diagnostic | C1 | Covered |
| §3.2 — Reverse index `ObjectTypeNamesByType` | C2 | Covered |
| §3.2 — `AONT041 MultiRegisteredTypeInLink` | C3 | Covered |
| §3.3 — `RootExpression.ObjectTypeName` required param | A1 | Covered |
| §3.3 — `ObjectSetExpression.RootObjectTypeName` walk-to-root | A2 | Covered |
| §3.3 — `TraverseLinkExpression` override | A3 | Covered |
| §3.4 — `ObjectSet<T>` ctor threads descriptor name | D1 | Covered |
| §3.4 — `OntologyQueryService.GetObjectSet<T>` threads name | D2 | Covered |
| §3.5 — `PgVectorObjectSetProvider` read-path dispatch | E1, E2, E3 | Covered |
| §3.5 — `InMemoryObjectSetProvider` read-path partition switch | E4 | Covered |
| §3.5 — `TypeMapper.GetTableName<T>()` removal | E5 | Covered |
| §3.6 — `IOntologyQuery.GetObjectTypeNames<T>()` | D3 | Covered |
| §3.7 — `IObjectSetWriter` explicit-name overloads | F1 | Covered |
| §3.7 — `InMemoryObjectSetProvider` explicit-name overload impl | F2 | Covered |
| §3.7 — `PgVectorObjectSetProvider` explicit-name overload impl | F3 | Covered |
| §3.7 — `PgVectorObjectSetProvider` graph-backed default resolution | F4 | Covered |
| §3.7 — `PgVectorObjectSetProvider.EnsureSchemaAsync` name overload | F5 | Covered |
| §3.7 — `IngestionPipelineBuilder<T>.WriteTo(writer, name)` | F6 | Covered |
| §5.7 — End-to-end multi-registration integration test | G1 | Covered |
| §5.8 — Close #28 and #29 housekeeping | G2 | Post-merge |

## Open Decisions Resolved by This Plan

The design doc §7 lists four open questions. The plan resolves them as follows:

1. **Name validation regex.** **Any C# identifier** — `^[a-zA-Z_][a-zA-Z0-9_]*$`. Rationale: ensures safe dictionary keys and safe snake-case conversion at the provider layer. Validated in `ObjectTypeBuilder<T>` constructor. Task **B3**.

2. **`GetObjectTypeNames<T>()` ordering.** **Registration order** (stable across graph builds). Rationale: matches user mental model; deterministic for snapshot/regression tests. Implementation: the reverse index is a `Dictionary<Type, List<string>>` populated by iterating `allObjectTypes` in the order the builder returned them. Task **D3**.

3. **AONT040 message quality.** Plain-text error naming both CLR types and the conflicting descriptor name. Source-info threading is **out of scope** — deferred to post-2.4.1 as a Roslyn-generator-level enhancement.

4. **`.ValidFromState` + multi-registration interaction.** Each descriptor has its own lifecycle projection, so no special handling required. Locked in via a single test in Task **B2** (projection works for the explicitly-named case).

## Track A — Expression Tree Foundation (3 tasks)

All Track A tasks operate on these files. They are sequenced within the track (A1 blocks A2, A3).

**Production files:**
- `src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs`

**Test files:**
- `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetExpressionTests.cs`

---

### Task A1: `RootExpression.ObjectTypeName` required constructor parameter

**Phase:** RED → GREEN

1. [RED] Write test: `RootExpression_Constructor_RequiresObjectTypeName`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetExpressionTests.cs`
   - Body: assert `new RootExpression(typeof(string), "trading_documents").ObjectTypeName == "trading_documents"` and `new RootExpression(typeof(string), null!)` throws `ArgumentNullException`
   - Expected failure: `RootExpression` only has a `(Type)` constructor; no `ObjectTypeName` property exists

2. [GREEN] Update `RootExpression`
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs`
   - Add required parameter to constructor: `public RootExpression(Type objectType, string objectTypeName)`
   - Add property: `public string ObjectTypeName { get; }`
   - Validate `ArgumentNullException.ThrowIfNull(objectTypeName)` in constructor
   - Update `ObjectSet<T>` ctor at `ObjectSet.cs:23` to temporarily pass `typeof(T).Name` as the name (proper threading happens in D1)
   - Update any other direct `RootExpression` construction sites discovered during compile (if any exist in tests, update those tests to supply a default name)

**Dependencies:** None
**Parallelizable:** Yes (with B1, B2, B3, F1 — independent files)

---

### Task A2: `ObjectSetExpression.RootObjectTypeName` walk-to-root computed property

**Phase:** RED → GREEN

1. [RED] Write tests in `ObjectSetExpressionTests.cs`:
   - `FilterExpression_RootObjectTypeName_WalksToSourceRoot` — construct `new FilterExpression(new RootExpression(typeof(Foo), "foo_table"), x => true)`, assert `.RootObjectTypeName == "foo_table"`
   - `InterfaceNarrowExpression_RootObjectTypeName_WalksToSourceRoot` — analogous
   - `IncludeExpression_RootObjectTypeName_WalksToSourceRoot` — analogous
   - `RawFilterExpression_RootObjectTypeName_WalksToSourceRoot` — analogous
   - `SimilarityExpression_RootObjectTypeName_WalksToSourceRoot` — analogous; this is the Basileus call site
   - `ComposedExpression_Root_Filter_Similarity_ReturnsRootName` — nested: `Similarity(Filter(Root))` walks two hops
   - Expected failure: `RootObjectTypeName` property does not exist on `ObjectSetExpression`

2. [GREEN] Add walk-to-root property on the base class
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs`
   - Add virtual property on `ObjectSetExpression`:
     ```csharp
     public virtual string RootObjectTypeName => WalkToRoot(this).ObjectTypeName;

     private static RootExpression WalkToRoot(ObjectSetExpression expr) => expr switch
     {
         RootExpression root => root,
         FilterExpression f => WalkToRoot(f.Source),
         InterfaceNarrowExpression i => WalkToRoot(i.Source),
         RawFilterExpression r => WalkToRoot(r.Source),
         IncludeExpression i => WalkToRoot(i.Source),
         SimilarityExpression s => WalkToRoot(s.Source),
         TraverseLinkExpression t => WalkToRoot(t.Source), // overridden in A3
         _ => throw new InvalidOperationException($"Unknown expression type: {expr.GetType().Name}")
     };
     ```

**Dependencies:** A1
**Parallelizable:** No (same file as A1)

---

### Task A3: `TraverseLinkExpression.RootObjectTypeName` override

**Phase:** RED → GREEN

1. [RED] Write test: `TraverseLinkExpression_RootObjectTypeName_ReturnsLinkedTypeName`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetExpressionTests.cs`
   - Body: construct `new TraverseLinkExpression(new RootExpression(typeof(Position), "positions"), "Orders", typeof(TradeOrder))`, assert `.RootObjectTypeName == "TradeOrder"` (not `"positions"`)
   - Expected failure: default walk-to-root returns the source root's name, which is the wrong answer after traversal
   - Rationale: under Option X multi-registered types cannot be link targets (AONT041), so `typeof(TLinked).Name` is unambiguous

2. [GREEN] Override `RootObjectTypeName` on `TraverseLinkExpression`
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSetExpression.cs`
   - Add to `TraverseLinkExpression`:
     ```csharp
     public override string RootObjectTypeName => ObjectType.Name;
     ```
   - Note: `ObjectType` on `TraverseLinkExpression` is already set to `linkedType` (see ctor at `ObjectSetExpression.cs:50`), so this reads the target CLR type's name directly

**Dependencies:** A2
**Parallelizable:** No (same file)

---

## Track B — Builder Layer (3 tasks)

**Production files:**
- `src/Strategos.Ontology/Builder/IOntologyBuilder.cs`
- `src/Strategos.Ontology/Builder/OntologyBuilder.cs`
- `src/Strategos.Ontology/Builder/ObjectTypeBuilder.cs`

**Test files:**
- `src/Strategos.Ontology.Tests/Builder/ObjectTypeBuilderTests.cs`
- `src/Strategos.Ontology.Tests/Builder/OntologyBuilderTests.cs`

---

### Task B1: `IOntologyBuilder.Object<T>(string? name, config)` overload

**Phase:** RED → GREEN

1. [RED] Write test: `OntologyBuilder_ObjectWithExplicitName_RegistersDescriptorWithThatName`
   - File: `src/Strategos.Ontology.Tests/Builder/OntologyBuilderTests.cs`
   - Body: create `OntologyBuilder("trading")`, call `builder.Object<SemanticDocument>("trading_documents", obj => { })`, inspect `builder.ObjectTypes`, assert `ObjectTypes.Single().Name == "trading_documents"`
   - Expected failure: no `Object<T>(string?, Action<...>)` overload exists

2. [GREEN] Add overload to interface and implementation
   - File: `src/Strategos.Ontology/Builder/IOntologyBuilder.cs` — add:
     ```csharp
     void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure) where T : class;
     ```
   - File: `src/Strategos.Ontology/Builder/OntologyBuilder.cs` — add:
     ```csharp
     public void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure) where T : class
     {
         var builder = new ObjectTypeBuilder<T>(domainName, explicitName: name);
         configure(builder);
         _objectTypes.Add(builder.Build());
     }
     ```
   - Ensure the parameterless `Object<T>(config)` overload still works (it should delegate to the new one with `name: null`)

**Dependencies:** None
**Parallelizable:** Yes (with A, F1)

---

### Task B2: `ObjectTypeBuilder<T>` stores explicit name and uses it in `Build()`

**Phase:** RED → GREEN

1. [RED] Write tests in `src/Strategos.Ontology.Tests/Builder/ObjectTypeBuilderTests.cs`:
   - `ObjectTypeBuilder_WithExplicitName_UsesExplicitNameInDescriptor` — construct `new ObjectTypeBuilder<Foo>("mydomain", "custom_name")`, call `Build()`, assert `descriptor.Name == "custom_name"` (CLR type is still `typeof(Foo)`)
   - `ObjectTypeBuilder_WithNullExplicitName_FallsBackToTypeofTName` — regression guard: `new ObjectTypeBuilder<Foo>("mydomain", explicitName: null)` → `descriptor.Name == "Foo"`
   - `ObjectTypeBuilder_WithExplicitName_LifecycleProjectionAppliesToThatDescriptor` — locks in the §7 open question: declare a lifecycle + `.ValidFromState()` on an action under an explicit name; assert the projection landed correctly on the explicit-named descriptor
   - Expected failure: `ObjectTypeBuilder<T>` constructor has only one parameter (`domainName`)

2. [GREEN] Update `ObjectTypeBuilder<T>`
   - File: `src/Strategos.Ontology/Builder/ObjectTypeBuilder.cs`
   - Update primary constructor: `internal sealed class ObjectTypeBuilder<T>(string domainName, string? explicitName = null) : IObjectTypeBuilder<T>`
   - Update `Build()` at line 136: `var descriptorName = explicitName ?? typeof(T).Name;` → `return new(descriptorName, typeof(T), domainName) { ... };`

**Dependencies:** B1
**Parallelizable:** No (same file)

---

### Task B3: Explicit-name regex validation in `ObjectTypeBuilder<T>` constructor

**Phase:** RED → GREEN

1. [RED] Write tests in `ObjectTypeBuilderTests.cs`:
   - `ObjectTypeBuilder_WithInvalidExplicitName_ThrowsArgumentException` — parameterized over `"has spaces"`, `"starts-with-hyphen"`, `"1starts_with_digit"`, `"has.dots"`; each should throw `ArgumentException` with a message naming the regex
   - `ObjectTypeBuilder_WithValidExplicitName_Succeeds` — parameterized over `"trading_documents"`, `"KnowledgeChunk"`, `"_underscore_start"`, `"snake_case_123"`
   - Expected failure: constructor accepts any name without validation

2. [GREEN] Add validation at construction time
   - File: `src/Strategos.Ontology/Builder/ObjectTypeBuilder.cs`
   - At the top of the constructor body (or primary constructor init):
     ```csharp
     private static readonly System.Text.RegularExpressions.Regex _nameRegex =
         new("^[a-zA-Z_][a-zA-Z0-9_]*$", System.Text.RegularExpressions.RegexOptions.Compiled);

     // In constructor:
     if (explicitName is not null && !_nameRegex.IsMatch(explicitName))
     {
         throw new ArgumentException(
             $"Object type name '{explicitName}' is not a valid identifier. " +
             $"Names must match ^[a-zA-Z_][a-zA-Z0-9_]*$ (C# identifier rules).",
             nameof(explicitName));
     }
     ```

**Dependencies:** B2
**Parallelizable:** No (same file)

---

## Track C — Graph Freeze Invariants (3 tasks)

**Production files:**
- `src/Strategos.Ontology/OntologyGraphBuilder.cs`
- `src/Strategos.Ontology/OntologyGraph.cs`
- `src/Strategos.Ontology/Diagnostics/OntologyDiagnostics.cs` (or wherever AONT codes are defined)

**Test files:**
- `src/Strategos.Ontology.Tests/OntologyGraphBuilderTests.cs`

---

### Task C1: `AONT040 DuplicateObjectTypeName` diagnostic

**Phase:** RED → GREEN

1. [RED] Write tests:
   - `GraphBuilder_WithDuplicateDescriptorNameInSameDomain_ThrowsAONT040`
     - Body: register `Object<Foo>("shared_name", ...)` twice in the same domain; call `Build()`; assert `OntologyCompositionException` (or `InvalidOperationException`, matching existing convention) naming the conflicting `shared_name` and both CLR types
   - `GraphBuilder_WithSameDescriptorNameAcrossDifferentDomains_Succeeds`
     - Body: register `Object<Foo>("shared_name", ...)` in domain A and `Object<Bar>("shared_name", ...)` in domain B; call `Build()`; assert no exception
   - Expected failure: today this fails with a cryptic `ArgumentException` from `ToDictionary`

2. [GREEN] Replace implicit failure with explicit diagnostic
   - File: `src/Strategos.Ontology/OntologyGraphBuilder.cs`
   - Before line 60 (`g.ToDictionary(ot => ot.Name)`), add a duplicate-name check per domain:
     ```csharp
     foreach (var group in allObjectTypes.GroupBy(ot => ot.DomainName))
     {
         var seen = new Dictionary<string, ObjectTypeDescriptor>();
         foreach (var descriptor in group)
         {
             if (seen.TryGetValue(descriptor.Name, out var existing))
             {
                 throw new OntologyCompositionException(
                     $"AONT040: Object type name '{descriptor.Name}' is registered twice in domain '{group.Key}'. " +
                     $"First registration: CLR type '{existing.ClrType.FullName}'. " +
                     $"Second registration: CLR type '{descriptor.ClrType.FullName}'. " +
                     $"Either remove one registration, or specify distinct names via Object<T>(\"name\", ...).");
             }
             seen[descriptor.Name] = descriptor;
         }
     }
     ```
   - Verify the existing `ToDictionary` at line 61 now always succeeds (the duplicate check is before it)
   - If `OntologyCompositionException` doesn't exist, use `InvalidOperationException` — check the existing codebase for the convention

**Dependencies:** B1 (needs the builder overload to construct duplicates in the test)
**Parallelizable:** Yes (with C2 once B is done — different files)

---

### Task C2: `OntologyGraph.ObjectTypeNamesByType` reverse index

**Phase:** RED → GREEN

1. [RED] Write tests in `OntologyGraphBuilderTests.cs`:
   - `OntologyGraph_ObjectTypeNamesByType_PopulatedForSingleRegistration` — one `Object<Foo>(...)` registration → `graph.ObjectTypeNamesByType[typeof(Foo)]` returns `["Foo"]`
   - `OntologyGraph_ObjectTypeNamesByType_PopulatedForMultiRegistration` — `Object<Foo>("a", ...)` and `Object<Foo>("b", ...)` → returns `["a", "b"]` in registration order
   - `OntologyGraph_ObjectTypeNamesByType_UnregisteredTypeReturnsEmpty` — `graph.ObjectTypeNamesByType.GetValueOrDefault(typeof(Bar)) ?? []` returns empty
   - Expected failure: property does not exist on `OntologyGraph`

2. [GREEN] Add reverse index
   - File: `src/Strategos.Ontology/OntologyGraph.cs`
   - Add property: `public IReadOnlyDictionary<Type, IReadOnlyList<string>> ObjectTypeNamesByType { get; }`
   - Accept in constructor as a new parameter
   - File: `src/Strategos.Ontology/OntologyGraphBuilder.cs`
   - Build the index after the duplicate check:
     ```csharp
     var namesByType = allObjectTypes
         .GroupBy(ot => ot.ClrType)
         .ToDictionary(
             g => g.Key,
             g => (IReadOnlyList<string>)g.Select(ot => ot.Name).ToList().AsReadOnly());
     ```
   - Pass `namesByType` into the `OntologyGraph` constructor

**Dependencies:** B1 (for multi-registration test setup)
**Parallelizable:** Yes with C1 after B

---

### Task C3: `AONT041 MultiRegisteredTypeInLink` check

**Phase:** RED → GREEN

1. [RED] Write tests:
   - `GraphBuilder_WithMultiRegisteredTypeAsLinkTarget_ThrowsAONT041`
     - Body: register `Object<SemanticDocument>("a", ...)` and `Object<SemanticDocument>("b", ...)`; in a different type, declare `HasMany<SemanticDocument>("Documents")`; call `Build()`; assert exception naming `SemanticDocument`, both registrations (`a`, `b`), and the referencing link (`Documents`)
   - `GraphBuilder_WithMultiRegisteredTypeAsLinkSource_ThrowsAONT041`
     - Body: same CLR type, but the multi-registered type declares an outgoing link; assert AONT041 fires
   - `GraphBuilder_WithMultiRegisteredLeafType_NoLinks_Succeeds`
     - Positive case: register `SemanticDocument` twice with no structural link references anywhere; assert `Build()` succeeds (this is the Basileus happy path)
   - Expected failure: no AONT041 check exists

2. [GREEN] Add the invariant check
   - File: `src/Strategos.Ontology/OntologyGraphBuilder.cs`
   - After the reverse index is built (C2), add a new private method:
     ```csharp
     private static void ValidateMultiRegisteredTypesNotInLinks(
         IReadOnlyList<ObjectTypeDescriptor> allObjectTypes,
         IReadOnlyDictionary<Type, IReadOnlyList<string>> namesByType)
     {
         var multiRegistered = namesByType
             .Where(kvp => kvp.Value.Count > 1)
             .Select(kvp => kvp.Key)
             .ToHashSet();

         if (multiRegistered.Count == 0) return;

         foreach (var descriptor in allObjectTypes)
         {
             foreach (var link in descriptor.Links)
             {
                 if (multiRegistered.Contains(link.TargetType))
                 {
                     var names = namesByType[link.TargetType];
                     throw new OntologyCompositionException(
                         $"AONT041: CLR type '{link.TargetType.FullName}' has multiple registrations " +
                         $"({string.Join(", ", names.Select(n => $"'{n}'"))}) but is also referenced as a link target " +
                         $"in '{descriptor.Name}.{link.Name}'. Multi-registered types cannot participate in structural " +
                         $"links. See #32 for a future relaxation path.");
                 }
             }

             if (multiRegistered.Contains(descriptor.ClrType) && descriptor.Links.Count > 0)
             {
                 var names = namesByType[descriptor.ClrType];
                 throw new OntologyCompositionException(
                     $"AONT041: CLR type '{descriptor.ClrType.FullName}' has multiple registrations " +
                     $"({string.Join(", ", names.Select(n => $"'{n}'"))}) but also declares outgoing links " +
                     $"({string.Join(", ", descriptor.Links.Select(l => l.Name))}). Multi-registered types cannot " +
                     $"participate in structural links. See #32 for a future relaxation path.");
             }
         }
     }
     ```
   - Call this after `ValidateInverseLinks(allObjectTypes);` at line 72

**Dependencies:** C2 (needs the reverse index)
**Parallelizable:** No (same file as C1, C2)

---

## Track D — Query Service + ObjectSet Wiring (3 tasks)

**Production files:**
- `src/Strategos.Ontology/ObjectSets/ObjectSet.cs`
- `src/Strategos.Ontology/Query/OntologyQueryService.cs`
- `src/Strategos.Ontology/Query/IOntologyQuery.cs`

**Test files:**
- `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTests.cs`
- `src/Strategos.Ontology.Tests/Query/OntologyQueryServiceTests.cs`

---

### Task D1: `ObjectSet<T>` ctor takes `descriptorName` and threads to `RootExpression`

**Phase:** RED → GREEN

1. [RED] Write test: `ObjectSet_Constructor_ThreadsDescriptorNameIntoRootExpression`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/ObjectSetTests.cs`
   - Body: construct `new ObjectSet<Foo>("trading_documents", mockProvider, mockDispatcher, mockEventStream)`, inspect `.Expression`, cast to `RootExpression`, assert `root.ObjectTypeName == "trading_documents"`
   - Expected failure: `ObjectSet<T>` has no ctor accepting a descriptor name

2. [GREEN] Update `ObjectSet<T>` ctor
   - File: `src/Strategos.Ontology/ObjectSets/ObjectSet.cs`
   - Replace the existing public ctor at line 22:
     ```csharp
     public ObjectSet(
         string descriptorName,
         IObjectSetProvider provider,
         IActionDispatcher actionDispatcher,
         IEventStreamProvider eventStreamProvider)
         : this(new RootExpression(typeof(T), descriptorName), provider, actionDispatcher, eventStreamProvider)
     {
         ArgumentNullException.ThrowIfNull(descriptorName);
     }
     ```
   - Audit and update any test code that constructs `ObjectSet<T>` directly (grep for `new ObjectSet<`); supply `typeof(T).Name` as the descriptor name in legacy test construction to preserve existing behavior

**Dependencies:** A1
**Parallelizable:** No (transitive on expression tree)

---

### Task D2: `OntologyQueryService.GetObjectSet<T>` threads descriptor name

**Phase:** RED → GREEN

1. [RED] Write test: `GetObjectSet_ThreadsDescriptorNameIntoRootExpression`
   - File: `src/Strategos.Ontology.Tests/Query/OntologyQueryServiceTests.cs`
   - Body: build an ontology with `Object<Foo>("custom_name", ...)`, construct `OntologyQueryService` with mock provider, call `query.GetObjectSet<Foo>("custom_name")`, inspect `objectSet.Expression`, cast to `RootExpression`, assert `root.ObjectTypeName == "custom_name"`
   - Follow-up test: `GetObjectSet_WithMultiRegistration_ReturnsDistinctRootExpressionsPerName`
     - Register `Object<Foo>("a", ...)` and `Object<Foo>("b", ...)`; call `GetObjectSet<Foo>("a")` and `GetObjectSet<Foo>("b")`; assert `root.ObjectTypeName` differs between the two
   - Expected failure: the root expression always has `typeof(T).Name`, not the passed descriptor name

2. [GREEN] Update `OntologyQueryService.GetObjectSet<T>` at `OntologyQueryService.cs:49-68`
   - Replace the `return new ObjectSet<T>(...)` at line 67 with:
     ```csharp
     return new ObjectSet<T>(
         descriptorName: ot.Name,
         _objectSetProvider,
         _actionDispatcher,
         _eventStreamProvider);
     ```

**Dependencies:** B1, B2 (for test setup with explicit names), D1 (for the ctor)
**Parallelizable:** No

---

### Task D3: `IOntologyQuery.GetObjectTypeNames<T>()` public reverse-index API

**Phase:** RED → GREEN

1. [RED] Write tests in `OntologyQueryServiceTests.cs`:
   - `GetObjectTypeNames_SingleRegistration_ReturnsOneName` — register `Object<Foo>(...)`; assert `query.GetObjectTypeNames<Foo>() == ["Foo"]`
   - `GetObjectTypeNames_MultiRegistration_ReturnsAllNamesInRegistrationOrder` — register `Object<Foo>("a", ...)` then `Object<Foo>("b", ...)`; assert `query.GetObjectTypeNames<Foo>() == ["a", "b"]`
   - `GetObjectTypeNames_UnregisteredType_ReturnsEmptyList` — no registration; assert empty list
   - Expected failure: method does not exist on `IOntologyQuery`

2. [GREEN] Add method
   - File: `src/Strategos.Ontology/Query/IOntologyQuery.cs` — add:
     ```csharp
     IReadOnlyList<string> GetObjectTypeNames<T>() where T : class;
     ```
   - File: `src/Strategos.Ontology/Query/OntologyQueryService.cs` — add:
     ```csharp
     public IReadOnlyList<string> GetObjectTypeNames<T>() where T : class
     {
         return graph.ObjectTypeNamesByType.TryGetValue(typeof(T), out var names)
             ? names
             : Array.Empty<string>();
     }
     ```

**Dependencies:** C2 (reverse index), B1 (multi-registration in test setup)
**Parallelizable:** Yes with D2 after C2

---

## Track E — Read-Path Provider Dispatch (5 tasks)

**Production files:**
- `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs`
- `src/Strategos.Ontology.Npgsql/Internal/TypeMapper.cs`
- `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`

**Test files:**
- `src/Strategos.Ontology.Npgsql.Tests/PgVectorObjectSetProviderTests.cs` (or wherever existing read tests live)
- `src/Strategos.Ontology.Tests/ObjectSets/InMemoryObjectSetProviderTests.cs`

---

### Task E1: `PgVectorObjectSetProvider.ExecuteSimilarityAsync` reads descriptor name from expression

**Phase:** RED → GREEN

1. [RED] Write test: `ExecuteSimilarityAsync_UsesDescriptorNameFromExpression_NotTypeofTName`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorObjectSetProviderTests.cs`
   - Body: construct a `SimilarityExpression` whose source is `new RootExpression(typeof(SemanticDocument), "trading_documents")`; spy on the generated SQL via an injected `NpgsqlDataSource` mock or by calling `SqlGenerator.BuildSimilarityQuery` directly; assert the `FROM` clause references `trading_documents`, not `semantic_document`
   - Expected failure: `ExecuteSimilarityAsync` calls `TypeMapper.GetTableName<T>()` at line 60 which returns `semantic_document`

2. [GREEN] Update `ExecuteSimilarityAsync` at `PgVectorObjectSetProvider.cs:60`
   - Replace `var tableName = TypeMapper.GetTableName<T>();` with:
     ```csharp
     var tableName = TypeMapper.ToSnakeCase(expression.RootObjectTypeName);
     ```

**Dependencies:** A1, A2
**Parallelizable:** Yes with E2, E3 after A

---

### Task E2: `PgVectorObjectSetProvider.ExecuteAsync` reads descriptor name from expression

**Phase:** RED → GREEN

1. [RED] Write test: `ExecuteAsync_UsesDescriptorNameFromExpression_NotTypeofTName`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorObjectSetProviderTests.cs`
   - Body: construct a non-similarity expression (a plain `RootExpression` or `FilterExpression`) with explicit descriptor name; assert the generated `FROM` clause uses the descriptor name
   - Expected failure: line 122 uses `TypeMapper.GetTableName<T>()`

2. [GREEN] Update `ExecuteAsync` at `PgVectorObjectSetProvider.cs:122`
   - Replace with `var tableName = TypeMapper.ToSnakeCase(expression.RootObjectTypeName);`

**Dependencies:** A1, A2
**Parallelizable:** Yes with E1, E3

---

### Task E3: `PgVectorObjectSetProvider.StreamAsync` reads descriptor name from expression

**Phase:** RED → GREEN

1. [RED] Write test: `StreamAsync_UsesDescriptorNameFromExpression_NotTypeofTName`
   - Analogous to E2 but for `StreamAsync`
   - Expected failure: line 157 uses `TypeMapper.GetTableName<T>()`

2. [GREEN] Update `StreamAsync` at `PgVectorObjectSetProvider.cs:157`
   - Same replacement as E1 and E2

**Dependencies:** A1, A2
**Parallelizable:** Yes with E1, E2

---

### Task E4: `InMemoryObjectSetProvider` partitions by descriptor name

**Phase:** RED → GREEN → REFACTOR

1. [RED] Write tests in `src/Strategos.Ontology.Tests/ObjectSets/InMemoryObjectSetProviderTests.cs`:
   - `InMemoryProvider_PartitionsByDescriptorName_NotClrType`
     - Body: seed an item under name "trading_documents"; query via an `ObjectSet<T>` whose root has name "knowledge_documents"; assert result is empty (different partition)
   - `InMemoryProvider_DefaultSeed_UsesTypeofTName` — regression guard: `Seed<Foo>(item, "content")` (no name) → queries via `RootExpression(typeof(Foo), "Foo")` find it
   - Expected failure: provider partitions by `typeof(T)`, not by descriptor name

2. [GREEN] Update `InMemoryObjectSetProvider`
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`
   - Change field types from `ConcurrentDictionary<Type, List<object>>` to `ConcurrentDictionary<string, List<object>>` (all three fields: `_items`, `_searchableContent`, `_embeddings`)
   - Update `Seed<T>` signature: `public void Seed<T>(T item, string searchableContent, string? descriptorName = null) where T : class`
     - Key: `var key = descriptorName ?? typeof(T).Name;`
   - Update `ExecuteAsync<T>`, `StreamAsync<T>`, `ExecuteSimilarityAsync<T>` to read the partition key from `expression.RootObjectTypeName`
   - Retain the `IEmbeddingProvider` cosine path unchanged (it only affects scoring, not partitioning)

3. [REFACTOR] Verify all existing tests using `Seed<T>(item, content)` still pass unchanged (no `descriptorName` arg, falls back to `typeof(T).Name`, matches default root expression names)

**Dependencies:** A1, A2
**Parallelizable:** Yes with E1-E3 (different file)

---

### Task E5: Remove `TypeMapper.GetTableName<T>()`

**Phase:** REFACTOR

1. [REFACTOR] Delete the method
   - File: `src/Strategos.Ontology.Npgsql/Internal/TypeMapper.cs`
   - Remove lines 50-53 (the `GetTableName<T>()` method)
   - `ToSnakeCase(string)` is retained (it's now called directly by the provider)
   - Verify the build succeeds — if any test or production code still references `GetTableName<T>`, fix that call site to use `ToSnakeCase(expression.RootObjectTypeName)` or (for write-path pre-F2/F3) a temporary `ToSnakeCase(typeof(T).Name)` that will be replaced in Track F

2. [REFACTOR] Add compile-time guard test: `TypeMapper_GetTableName_Of_T_Is_Removed`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorObjectSetProviderTests.cs`
   - Body: `typeof(TypeMapper).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Any(m => m.Name == "GetTableName" && m.IsGenericMethod).Should().BeFalse()` (or TUnit equivalent)
   - This locks the footgun removal in place permanently

**Dependencies:** E1, E2, E3, F3 (all read and write call sites updated first)
**Parallelizable:** No

---

## Track F — Write-Path Provider Dispatch (6 tasks)

**Production files:**
- `src/Strategos.Ontology/ObjectSets/IObjectSetWriter.cs`
- `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`
- `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs`
- `src/Strategos.Ontology/Ingestion/IngestionPipeline.cs`
- `src/Strategos.Ontology/Ingestion/IngestionPipelineBuilder.cs`

**Test files:**
- `src/Strategos.Ontology.Tests/ObjectSets/IObjectSetWriterTests.cs` (new)
- `src/Strategos.Ontology.Npgsql.Tests/PgVectorWriteTests.cs`
- `src/Strategos.Ontology.Tests/Ingestion/IngestionPipelineTests.cs`

---

### Task F1: `IObjectSetWriter` gains explicit-name overloads

**Phase:** RED → GREEN

1. [RED] Write test: `IObjectSetWriter_HasExplicitNameOverloads_ForStoreAsyncAndStoreBatchAsync`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/IObjectSetWriterTests.cs` (new file)
   - Body: reflection assertion that `IObjectSetWriter` exposes 4 methods — `StoreAsync<T>(T, CancellationToken)`, `StoreAsync<T>(string, T, CancellationToken)`, `StoreBatchAsync<T>(IReadOnlyList<T>, CancellationToken)`, `StoreBatchAsync<T>(string, IReadOnlyList<T>, CancellationToken)`
   - Expected failure: only the default overloads exist

2. [GREEN] Add overloads to `IObjectSetWriter`
   - File: `src/Strategos.Ontology/ObjectSets/IObjectSetWriter.cs`
   - Add two methods alongside the existing ones:
     ```csharp
     Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class;
     Task StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct = default) where T : class;
     ```
   - The interface change breaks `InMemoryObjectSetProvider` and `PgVectorObjectSetProvider`; those are fixed in F2 and F3 respectively. To avoid a broken build between F1 GREEN and F2/F3, F1 also adds **throwing stubs** to both providers: `public Task StoreAsync<T>(string descriptorName, T item, CancellationToken ct = default) where T : class => throw new NotImplementedException();` — F2 and F3 replace these with real implementations

**Dependencies:** None (interface-only change)
**Parallelizable:** Yes (with A, B, C)

---

### Task F2: `InMemoryObjectSetProvider` implements explicit-name write overloads

**Phase:** RED → GREEN

1. [RED] Write tests in `IObjectSetWriterTests.cs`:
   - `InMemoryWriter_StoreAsync_ExplicitName_UsesSuppliedName`
     - Body: `provider.StoreAsync<Foo>("my_partition", item, ct)`; query via `ObjectSet<T>` with root name "my_partition"; assert item is found; query with root name "other_partition"; assert empty
   - `InMemoryWriter_StoreBatchAsync_ExplicitName_PartitionsByName`
     - Body: batch store under "my_partition"; assert all items queryable under that name only
   - `InMemoryWriter_StoreAsync_DefaultOverload_UsesTypeofTName` (regression)
     - Body: `provider.StoreAsync(item, ct)` (no name) → queryable under `typeof(T).Name`
   - Expected failure: explicit-name overloads throw `NotImplementedException` from F1 stubs

2. [GREEN] Implement overloads in `InMemoryObjectSetProvider`
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`
   - `StoreAsync<T>(string descriptorName, T item, CancellationToken ct)`: validate name, write to `_items[descriptorName]` / `_searchableContent[descriptorName]` / `_embeddings[descriptorName]`
   - `StoreBatchAsync<T>(string descriptorName, IReadOnlyList<T> items, CancellationToken ct)`: iterate and call the explicit-name `StoreAsync` variant
   - Default overload (existing `StoreAsync<T>(T item, ct)`) delegates to the explicit overload with `descriptorName: typeof(T).Name`
   - Refactor to share code between default and explicit paths

**Dependencies:** F1, E4 (E4 already switched the partition key to string)
**Parallelizable:** Yes with F3 after F1 + E4

---

### Task F3: `PgVectorObjectSetProvider` implements explicit-name write overloads

**Phase:** RED → GREEN

1. [RED] Write tests in `src/Strategos.Ontology.Npgsql.Tests/PgVectorWriteTests.cs`:
   - `PgVectorProvider_StoreAsync_ExplicitName_WritesToNamedTable`
     - Body: invoke `StoreAsync<SemanticDocument>("trading_documents", item, ct)`; verify the generated `INSERT` SQL targets `trading_documents` (use SQL capture via command interception or assert via `SqlGenerator.BuildInsertSql` directly)
   - `PgVectorProvider_StoreBatchAsync_ExplicitName_UsesCopyToNamedTable`
     - Body: invoke batch store with explicit name; assert the `COPY <schema>.trading_documents ... FROM STDIN` target
   - Expected failure: stubs throw `NotImplementedException` (from F1)

2. [GREEN] Implement overloads
   - File: `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs`
   - Extract the existing `StoreAsync<T>` body into a private helper `StoreAsyncCore<T>(string tableName, T item, CancellationToken ct)` that takes an already-resolved table name
   - Same for `StoreBatchAsync<T>` → `StoreBatchAsyncCore<T>(string tableName, ...)`
   - Public `StoreAsync<T>(string descriptorName, T item, CancellationToken ct)`: validate name, `var tableName = TypeMapper.ToSnakeCase(descriptorName);`, call `StoreAsyncCore`
   - Public `StoreBatchAsync<T>(string descriptorName, ...)`: analogous
   - Default overload (`StoreAsync<T>(T item, ...)`) — wire up to graph-backed resolution in **F4**; for now, temporarily retain `TypeMapper.ToSnakeCase(typeof(T).Name)` to keep existing tests passing until F4 lands

**Dependencies:** F1
**Parallelizable:** Yes with F2 after F1

---

### Task F4: `PgVectorObjectSetProvider` graph-backed default overload resolution

**Phase:** RED → GREEN

1. [RED] Write tests in `PgVectorWriteTests.cs`:
   - `PgVectorProvider_StoreAsync_DefaultOverload_ResolvesViaGraph_SingleRegistration`
     - Body: register `Object<Foo>("foo", ...)` (single registration), inject the graph into the provider via constructor, call `provider.StoreAsync(foo, ct)`; assert the generated INSERT targets `foo`
   - `PgVectorProvider_StoreAsync_DefaultOverload_WithMultiRegistration_ThrowsWithDiagnostic`
     - Body: register `Object<Foo>("a", ...)` and `Object<Foo>("b", ...)`; call `provider.StoreAsync(foo, ct)`; assert `InvalidOperationException` whose message names both registrations and instructs the caller to use the explicit-name overload
   - `PgVectorProvider_StoreAsync_DefaultOverload_FallsBackToTypeofTName_WhenGraphAbsent`
     - Body: construct the provider without the graph parameter (null); call `provider.StoreAsync(foo, ct)`; assert the generated INSERT targets `foo` (the `typeof(T).Name` fallback)
   - Expected failure: the default overload currently uses `typeof(T).Name` unconditionally (from F3 temporary behavior)

2. [GREEN] Update provider to accept optional graph + implement resolution
   - File: `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs`
   - Add optional constructor parameter: `OntologyGraph? graph = null` (keep the existing ctor shape; add the new one)
   - Store `_graph` as a private field
   - In default `StoreAsync<T>(T item, ...)`:
     ```csharp
     string descriptorName;
     if (_graph is not null && _graph.ObjectTypeNamesByType.TryGetValue(typeof(T), out var names))
     {
         if (names.Count == 0) descriptorName = typeof(T).Name;   // unregistered → fallback
         else if (names.Count == 1) descriptorName = names[0];
         else throw new InvalidOperationException(
             $"Type '{typeof(T).FullName}' has multiple registrations ({string.Join(", ", names.Select(n => $"'{n}'"))}). " +
             $"Use StoreAsync<T>(string descriptorName, T item, ct) to specify the target descriptor.");
     }
     else
     {
         descriptorName = typeof(T).Name;   // graph absent → fallback
     }
     return StoreAsyncCore<T>(TypeMapper.ToSnakeCase(descriptorName), item, ct);
     ```
   - Same logic for `StoreBatchAsync<T>` default overload
   - Update DI registration in `PgVectorServiceCollectionExtensions` (or equivalent) to pass the graph when available

**Dependencies:** F3, C2 (reverse index)
**Parallelizable:** No

---

### Task F5: `PgVectorObjectSetProvider.EnsureSchemaAsync(string? descriptorName)`

**Phase:** RED → GREEN

1. [RED] Write test: `EnsureSchemaAsync_WithExplicitName_CreatesNamedTable`
   - File: `src/Strategos.Ontology.Npgsql.Tests/PgVectorWriteTests.cs`
   - Body: call `provider.EnsureSchemaAsync<SemanticDocument>("trading_documents", ct)`; assert the generated DDL creates table `trading_documents`
   - Expected failure: `EnsureSchemaAsync<T>()` takes no name parameter and hardcodes `typeof(T).Name`

2. [GREEN] Update signature and body
   - File: `src/Strategos.Ontology.Npgsql/PgVectorObjectSetProvider.cs:248-259`
   - New signature: `public async Task EnsureSchemaAsync<T>(string? descriptorName = null, CancellationToken ct = default) where T : class`
   - Use the same graph-backed resolution pattern from F4 when `descriptorName is null`
   - Call `TypeMapper.ToSnakeCase(resolvedName)` to compute `tableName`

**Dependencies:** F4 (same graph-backed resolution helper)
**Parallelizable:** No

---

### Task F6: `IngestionPipelineBuilder<T>.WriteTo(writer, descriptorName)` overload

**Phase:** RED → GREEN

1. [RED] Write tests in `src/Strategos.Ontology.Tests/Ingestion/IngestionPipelineTests.cs`:
   - `IngestionPipeline_WriteToWithDescriptorName_CallsExplicitStoreBatchAsync`
     - Body: build a pipeline with `.WriteTo(writer, "trading_documents")`; execute over a single chunk; assert the mock writer's explicit-name `StoreBatchAsync<T>(string, IReadOnlyList<T>, CancellationToken)` was invoked with `"trading_documents"`
   - `IngestionPipeline_WriteToWithoutDescriptorName_CallsDefaultStoreBatchAsync` (regression)
     - Body: existing default path; assert the default-overload `StoreBatchAsync<T>(IReadOnlyList<T>, CancellationToken)` was invoked
   - Expected failure: no `WriteTo(writer, string)` overload exists

2. [GREEN] Update builder and pipeline
   - File: `src/Strategos.Ontology/Ingestion/IngestionPipelineBuilder.cs`
   - Add `public IngestionPipelineBuilder<T> WriteTo(IObjectSetWriter writer, string descriptorName)` alongside the existing `WriteTo(IObjectSetWriter writer)`
   - Store `_descriptorName: string?` on the builder
   - Pass `_descriptorName` into `IngestionPipeline<T>` via its internal constructor
   - File: `src/Strategos.Ontology/Ingestion/IngestionPipeline.cs`
   - Add `_descriptorName: string?` field
   - In `ExecuteCoreAsync` at line 120, branch:
     ```csharp
     if (_descriptorName is not null)
         await _writer.StoreBatchAsync<T>(_descriptorName, mappedItems, ct).ConfigureAwait(false);
     else
         await _writer.StoreBatchAsync<T>(mappedItems, ct).ConfigureAwait(false);
     ```

**Dependencies:** F1
**Parallelizable:** Yes with F2, F3 after F1

---

## Track G — End-to-End Integration + Housekeeping (2 tasks)

---

### Task G1: End-to-end multi-registration integration test

**Phase:** RED → GREEN (GREEN passes once all dependencies are met)

1. [RED] Write test: `EndToEnd_MultiRegistration_FluentSimilarityChain_IsolatesByDescriptorName`
   - File: `src/Strategos.Ontology.Tests/Query/OntologyQueryFluentSimilarityTests.cs` (extend the existing file)
   - Body:
     1. Build an ontology registering `SemanticDocument` twice under domain "basileus": `Object<SemanticDocument>("trading_documents", ...)` and `Object<SemanticDocument>("knowledge_documents", ...)`
     2. Configure `InMemoryObjectSetProvider` with a deterministic fake `IEmbeddingProvider`
     3. Seed two distinct sets of `SemanticDocument` items via the explicit-name `StoreAsync<T>(string, T, ct)` overload under each name
     4. Execute the full fluent chain: `query.GetObjectSet<SemanticDocument>("trading_documents").SimilarTo("market data").WithMinRelevance(0.0).Take(10).ExecuteAsync(ct)`
     5. Assert the result contains only the trading partition's items
     6. Repeat for `"knowledge_documents"`; assert the result contains only the knowledge partition's items
     7. Assert both result sets are disjoint — no cross-contamination
   - This test is the regression guard for #31 — if any of the five wiring points (builder, graph, expression, query service, provider) breaks, this test fails
   - Expected failure: depends on all prior tasks being complete; before then, likely fails at builder (multi-registration rejected), ObjectSet ctor (no descriptor name), or provider dispatch (wrong partition)

2. [GREEN] No new code; test passes once A, B, C, D, E, F are complete

**Dependencies:** A1-A3, B1-B3, C1-C3, D1-D3, E1-E5, F1-F6
**Parallelizable:** No (integration test, runs last)

---

### Task G2: Close #28 and #29 as delivered in 2.4.0

**Phase:** POST-MERGE (not code; workflow hygiene)

1. After 2.4.1 PR merges, run:
   ```bash
   gh issue close 28 --comment "Delivered in 2.4.0 via #30 (commit 9e47550). Fluent similarity chain (ObjectSet<T>.SimilarTo() + fluent setters + IOntologyQuery.GetObjectSet<T>) shipped as planned. The descriptor-name dispatch follow-up (#31) was fixed in 2.4.1 via the ontology-descriptor-name-dispatch design."

   gh issue close 29 --comment "Delivered in 2.4.0 via #30 (commit 9e47550). Lifecycle DSL sugar (InitialState/TerminalState/Transition overload) and .ValidFromState projection shipped as planned. GetActionsForState read-side was already correct per the 2.4.0 spec."
   ```

**Dependencies:** G1 passing + PR merge
**Parallelizable:** N/A (post-merge)

---

## Parallelization Graph

```
Group 1 (start in parallel):
    ├── A1 → A2 → A3                (expression tree foundation)
    ├── B1 → B2 → B3                (builder layer)
    └── F1                           (IObjectSetWriter interface)

Group 2 (after Group 1):
    ├── C1, C2 (parallel)            (graph freeze diagnostics + reverse index)
    │
    ├── D1 → D2                      (ObjectSet ctor + query service)
    │
    ├── E1, E2, E3 (parallel)        (PgVector read paths)
    │
    ├── E4                           (InMemory partition switch)
    │
    ├── F2                           (InMemory write overloads; needs E4)
    │
    └── F3                           (PgVector write overloads; needs F1)

Group 3 (after Group 2):
    ├── C3                           (AONT041; needs C2)
    ├── D3                           (GetObjectTypeNames; needs C2)
    ├── E5                           (TypeMapper cleanup; needs E1-E3 + F3)
    └── F4 → F5 → F6                 (graph-backed default + ensure schema + ingestion pipeline)

Group 4 (integration):
    └── G1                           (end-to-end; needs everything)

Group 5 (post-merge):
    └── G2                           (close #28, #29)
```

## Worktree Strategy

All tasks within Tracks A, B, and F1 can proceed in **three parallel worktrees** since they touch disjoint files:
- Worktree 1: Track A (`ObjectSetExpression.cs`)
- Worktree 2: Track B (`OntologyBuilder.cs`, `ObjectTypeBuilder.cs`, `IOntologyBuilder.cs`)
- Worktree 3: Track F1 (`IObjectSetWriter.cs` + stub additions to both providers)

After Group 1 lands on main, Tracks C, D, E, and F2–F6 can proceed in **four parallel worktrees**:
- Worktree 1: Track C (graph freeze — `OntologyGraphBuilder.cs`, `OntologyGraph.cs`)
- Worktree 2: Tracks D1–D3 (query service + ObjectSet — `OntologyQueryService.cs`, `ObjectSet.cs`, `IOntologyQuery.cs`)
- Worktree 3: Tracks E1–E4 (provider read paths — `PgVectorObjectSetProvider.cs`, `InMemoryObjectSetProvider.cs`)
- Worktree 4: Tracks F2–F6 (provider write paths + ingestion — overlaps Worktree 3 on provider files, so must sequence after E)

E5 and G1 run last on main after all worktrees merge.

## Risk & Mitigation

1. **Compile-level breakage between A1 GREEN and D1 GREEN.** A1 makes `RootExpression.ObjectTypeName` a required parameter. The only existing caller is `ObjectSet<T>(...)` at `ObjectSet.cs:23`, which A1's GREEN must update to pass `typeof(T).Name` as a placeholder until D1 lands. **Mitigation:** A1's GREEN acceptance criteria explicitly include "all tests in `Strategos.Ontology.Tests` compile and pass with the placeholder name." D1 replaces the placeholder with a threaded value.

2. **F1 interface change breaks both providers until F2/F3 ship.** **Mitigation:** F1's GREEN adds throwing `NotImplementedException` stubs to both providers to keep the build green; F2 and F3 replace the stubs.

3. **E5 removal of `TypeMapper.GetTableName<T>()` might break F3 pre-F4.** F3's default overload initially uses `TypeMapper.ToSnakeCase(typeof(T).Name)` directly (not `GetTableName<T>`) to avoid this dependency. **Mitigation:** E5 can land after F3 without issue; F3 never re-references `GetTableName<T>`.

4. **Existing `InMemoryObjectSetProvider` tests might break on E4's partition switch.** The partition is now keyed by string, not `Type`. **Mitigation:** E4's GREEN preserves `Seed<T>(item, content)` (no `descriptorName`) as equivalent to `Seed<T>(item, content, typeof(T).Name)`, and `ExecuteAsync<T>` etc. read from `expression.RootObjectTypeName` which defaults to `typeof(T).Name` when the ObjectSet is constructed via the legacy path. Concrete regression test: E4's test plan includes `InMemoryProvider_DefaultSeed_UsesTypeofTName`.

5. **Basileus post-release migration blocked on NuGet publish.** Strategos 2.4.1 must publish before Basileus can migrate. **Mitigation:** Basileus migration is tracked in a separate issue; this plan scopes to Strategos-only changes.

## Test Count Summary

| Track | New test count | Files touched |
|---|---|---|
| A — expression tree | 8 | 1 |
| B — builder | 5 | 3 |
| C — graph freeze | 7 | 2 |
| D — query service | 5 | 3 |
| E — read paths | 5 | 3 |
| F — write paths | 10 | 5 |
| G — integration | 1 | 1 |
| **Total** | **41** | **18** |

(Design doc §5 listed 34 test names; the expansion to 41 reflects the §3.7 write-path coverage that was added during plan drafting.)

## Success Criteria

1. All 41 new tests pass (TUnit with `dotnet test -- --treenode-filter "/*/*/*/TestName"`)
2. All existing tests in `Strategos.Ontology.Tests` and `Strategos.Ontology.Npgsql.Tests` continue to pass without modification (except for direct `new ObjectSet<T>(...)` test construction sites, which are updated mechanically)
3. `dotnet build` succeeds across all packages with no warnings elevated to errors
4. `dotnet format` clean per the 2.4.0 format-check convention
5. `AONT040` fires on duplicate descriptor names with a clear error message
6. `AONT041` fires on multi-registration + link participation with a clear error message + #32 reference
7. `TypeMapper.GetTableName<T>()` does not exist in the built assembly (reflection guard)
8. Issues #28 and #29 closed with delivery pointers to commit `9e47550`
9. Issue #31 closed with pointer to the 2.4.1 merge commit
10. Design doc `docs/designs/2026-04-08-ontology-descriptor-name-dispatch.md` remains the source of truth; any plan deviations documented in a §Plan Corrections appendix
