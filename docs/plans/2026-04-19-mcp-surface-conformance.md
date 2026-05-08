# Implementation Plan: MCP Surface Conformance

**Design:** `docs/designs/2026-04-19-mcp-surface-conformance.md`
**Date:** 2026-04-19
**Tasks:** 14
**Tracks:** 2 (A: Graph Versioning, B: Descriptor Upgrade + Meta Envelope)
**Parallelization:** Track B can begin once Track A Task A1 lands (Version property exists). Tasks A1–A5 are sequential within Track A. Tasks B1, B2, B3 are parallel-safe; B4–B9 sequential.

---

## Test invocation note

This repo uses TUnit on Microsoft Testing Platform — `dotnet test --filter` will fail. All test runs in this plan use:

```bash
dotnet test --project src/Strategos.Ontology.Tests/Strategos.Ontology.Tests.csproj
dotnet test --project src/Strategos.Ontology.MCP.Tests/Strategos.Ontology.MCP.Tests.csproj
```

To filter to a single test/class: append `-- --treenode-filter "/*/*/ClassName/*"`.

---

## Track A: Graph Versioning (#44)

### Task A1: `OntologyGraph.Version` property exists and returns deterministic hex
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Version_OnEmptyGraph_ReturnsLowercaseSha256Hex`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs` (new)
   - Build a graph with `OntologyGraphBuilder().Build()` (no domains), assert `graph.Version` is non-null, length 64, matches `^[0-9a-f]{64}$`
   - Expected failure: `OntologyGraph` has no `Version` property

2. **[RED]** Write test: `Version_BuiltTwice_ReturnsSameHash`
   - Same file
   - Build the same fixture DSL twice (helper method), assert the two `Version` strings are equal
   - Expected failure: same — property doesn't exist

3. **[GREEN]** Add `OntologyGraph.Version` backed by `OntologyGraphHasher.ComputeVersion(this)`
   - File: `src/Strategos.Ontology/OntologyGraph.cs` — add `public string Version { get; }` initialized in the constructor
   - File: `src/Strategos.Ontology/Internal/OntologyGraphHasher.cs` (new) — `internal static class OntologyGraphHasher` with `static string ComputeVersion(OntologyGraph graph)` that returns lowercase hex sha256 over a stable byte stream consisting only of `Domains.Select(d => d.DomainName)` (sorted) for now. Use `SHA256.HashData(...)` with `Convert.ToHexStringLower(bytes)`.

4. **[REFACTOR]** Extract a `WriteStableHeader(BinaryWriter, OntologyGraph)` private helper to make subsequent task additions clean

**Dependencies:** None
**Parallelizable:** No (Track A start)

---

### Task A2: Hasher covers ObjectType structure (properties, actions, links, events, lifecycle, interfaces)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Version_AddingObjectType_ChangesHash`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs`
   - Build fixture A (one domain, no types) and fixture B (one domain + one type), assert `versionA != versionB`

2. **[RED]** Write test: `Version_AddingProperty_ChangesHash` — same approach with two object-type fixtures differing only by an extra property

3. **[RED]** Write test: `Version_RenamingAction_ChangesHash` — fixtures with same shape but different action name on a type

4. **[RED]** Write test: `Version_AddingLink_ChangesHash` — fixtures differing by an `obj.HasMany<X>("...")` declaration

5. **[RED]** Write test: `Version_AddingEvent_ChangesHash` — fixtures differing by `.EmitsEvent<E>()`

6. **[RED]** Write test: `Version_LifecycleStateAddition_ChangesHash` — fixtures with different lifecycle states on the same type

7. **[RED]** Write test: `Version_ImplementedInterface_ChangesHash` — fixtures differing by interface implementation

8. **[GREEN]** Extend `OntologyGraphHasher.ComputeVersion` to walk `graph.ObjectTypes` (sorted by `(DomainName, Name)`) and stably serialize:
   - `(DomainName, Name, ParentTypeName ?? "")`
   - `Properties` sorted by `Name`: `(Name, Kind, ClrType.FullName ?? "", IsNullable, VectorDimensions ?? 0)`
   - `Actions` sorted by `Name`: `(Name, AcceptsType?.FullName ?? "", ReturnsType?.FullName ?? "", BindingType, IsReadOnly, Preconditions sorted by Description.Concat(Postconditions sorted by Description))`
   - `Links` sorted by `Name`: `(Name, TargetTypeName, Cardinality, EdgeProperties sorted by Name → (Name, Kind))`
   - `Events` sorted by `EventType.FullName`: `(EventType.FullName, Severity, MaterializedLinks sorted, UpdatedProperties sorted)`
   - `Lifecycle` (if present): states sorted; transitions sorted by `(From, To, Trigger ?? "")`
   - `ImplementedInterfaces` sorted by `Name`

9. **[REFACTOR]** Move per-section writers into named private methods (`WriteObjectType`, `WriteProperty`, `WriteAction`, etc.) for readability

**Dependencies:** A1
**Parallelizable:** No (sequential within Track A)

---

### Task A3: Hasher covers Interfaces, CrossDomainLinks, WorkflowChains
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Version_AddingInterface_ChangesHash` — fixtures differing by an `Interface<I>("Name")` registration

2. **[RED]** Write test: `Version_AddingCrossDomainLink_ChangesHash` — fixtures differing by a cross-domain link

3. **[RED]** Write test: `Version_AddingWorkflowChain_ChangesHash` — fixtures differing by a workflow chain registration

4. **[GREEN]** Extend `OntologyGraphHasher.ComputeVersion`:
   - `Interfaces` sorted by `Name`: `(Name, Properties sorted by Name → (Name, Kind, ClrType.FullName ?? ""))`
   - `CrossDomainLinks` sorted by `(SourceDomain, SourceTypeName, LinkName)`: `(SourceDomain, SourceTypeName, LinkName, TargetDomain, TargetTypeName, Cardinality, EdgeProperties sorted)`
   - `WorkflowChains` sorted by `WorkflowName`: `(WorkflowName, ConsumedType.FullName, ProducedType.FullName)`

5. **[REFACTOR]** None expected

**Dependencies:** A2
**Parallelizable:** No (sequential within Track A)

---

### Task A4: Hasher EXCLUDES `Description` text and `Warnings`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Version_ChangingActionDescription_DoesNotChangeHash`
   - Fixtures with same action but different `.Description("...")` text → `versionA == versionB`
   - Expected failure: hasher currently doesn't read Description (passes by accident); test pins the contract

2. **[RED]** Write test: `Version_ChangingLinkDescription_DoesNotChangeHash` — same shape with link descriptions

3. **[RED]** Write test: `Version_DifferingWarnings_DoesNotChangeHash`
   - Construct two graphs through the builder, one of which produces a warning (e.g., orphan interface implementation), assert `Version` matches the no-warning case for the same structural shape
   - If the warning-producing case has a different structural shape, build a controlled fixture using the internal `OntologyGraph` constructor with a synthetic `warnings` list

4. **[GREEN]** Verify the hasher does NOT read `Description` or `Warnings` from any descriptor; if it does (from prior tasks), remove those reads. Add an inline comment in `OntologyGraphHasher.ComputeVersion` explaining the exclusion (per design §4.1)

5. **[REFACTOR]** None expected

**Dependencies:** A3
**Parallelizable:** No (sequential within Track A)

---

### Task A5: Reference fixture pins a known hash (regression guard)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Version_ReferenceFixture_MatchesPinnedConstant`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs`
   - Build a small two-domain fixture (helper method `BuildReferenceFixture()`) — one domain with one object type containing one property + one action + one link; one cross-domain link; one workflow chain
   - Assert `graph.Version == "<TBD-pinned-constant>"`
   - Expected failure: pinned constant is initially `"REPLACE_ME"` so test fails

2. **[GREEN]** Run the test once to capture the actual hash, then replace the pinned constant in the test file with the captured value
   - Add a code comment above the constant: `// Pinned hash for the reference fixture. If this changes, the OntologyGraphHasher serialization shape has drifted — review the diff before updating.`

3. **[REFACTOR]** None expected

**Dependencies:** A4
**Parallelizable:** No (sequential within Track A; final task)

---

## Track B: Descriptor Upgrade + Meta Envelope (#40 + the integration glue)

### Task B1: `ToolAnnotations` record
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ToolAnnotations_Construction_StoresAllFourHints`
   - File: `src/Strategos.Ontology.MCP.Tests/ToolAnnotationsTests.cs` (new)
   - Construct `new ToolAnnotations(true, false, true, false)`, assert each property reflects input
   - Expected failure: `ToolAnnotations` type does not exist

2. **[RED]** Write test: `ToolAnnotations_RecordEquality_HoldsForSameInputs`
   - Two equal-input instances, assert `==` returns true (record value semantics)

3. **[GREEN]** Add `ToolAnnotations` record
   - File: `src/Strategos.Ontology.MCP/ToolAnnotations.cs` (new)
   - `public sealed record ToolAnnotations(bool ReadOnlyHint, bool DestructiveHint, bool IdempotentHint, bool OpenWorldHint);`

4. **[REFACTOR]** None expected

**Dependencies:** None
**Parallelizable:** Yes (Track B can start as soon as Task A1 lands; B1 itself is independent of A)

---

### Task B2: `OntologyToolDescriptor` upgraded with `Title`, `OutputSchema`, `Annotations`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `OntologyToolDescriptor_TwoArgConstructor_DefaultsAllNewFields`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyToolDescriptorTests.cs` (new)
   - `new OntologyToolDescriptor("name", "desc")` → `Title is null`, `OutputSchema is null`, `Annotations` is `new(false, false, false, false)`, `ConstraintSummaries.Count == 0`
   - Expected failure: properties don't exist

2. **[RED]** Write test: `OntologyToolDescriptor_WithTitle_StoresValue` — `desc with { Title = "T" }` round-trips

3. **[RED]** Write test: `OntologyToolDescriptor_WithAnnotations_StoresValue` — `desc with { Annotations = new(true, ...) }` round-trips

4. **[RED]** Write test: `OntologyToolDescriptor_WithOutputSchema_StoresValue` — `desc with { OutputSchema = element }` round-trips, where `element` is `JsonSerializer.SerializeToElement(new { type = "object" })`

5. **[GREEN]** Add init-only properties to `OntologyToolDescriptor`
   - File: `src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs`
   - `public string? Title { get; init; }`
   - `public JsonElement? OutputSchema { get; init; }`
   - `public ToolAnnotations Annotations { get; init; } = new(false, false, false, false);`

6. **[REFACTOR]** None expected

**Dependencies:** B1 (annotations type must exist)
**Parallelizable:** No (sequential after B1)

---

### Task B3: `JsonSchemaHelper.JsonSchemaFor<T>()` utility
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `JsonSchemaFor_PrimitiveType_ReturnsValidSchemaElement`
   - File: `src/Strategos.Ontology.MCP.Tests/Internal/JsonSchemaHelperTests.cs` (new)
   - `var schema = JsonSchemaHelper.JsonSchemaFor<string>(); var s = schema.GetRawText();`
   - Assert: `s` is non-empty and parseable as JSON containing `"type":"string"`
   - Expected failure: `JsonSchemaHelper` doesn't exist

2. **[RED]** Write test: `JsonSchemaFor_RecordType_ReturnsObjectSchemaWithProperties`
   - Use a local `record TestRecord(string A, int B);`
   - Assert `schema.GetRawText()` parses to JSON containing `"type":"object"` and property names `"A"` / `"B"`

3. **[GREEN]** Implement `JsonSchemaHelper`
   - File: `src/Strategos.Ontology.MCP/Internal/JsonSchemaHelper.cs` (new)
   - `internal static class JsonSchemaHelper { public static JsonElement JsonSchemaFor<T>() { var node = JsonSchemaExporter.GetJsonSchemaAsNode(JsonSerializerOptions.Default, typeof(T)); return JsonSerializer.SerializeToElement(node); } }`
   - Add `using System.Text.Json.Schema;`

4. **[REFACTOR]** None expected

**Dependencies:** None
**Parallelizable:** Yes (parallel-safe with B1, B2, A-track work)

---

### Task B4: `ResponseMeta` record
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ResponseMeta_Construction_StoresOntologyVersion`
   - File: `src/Strategos.Ontology.MCP.Tests/ResponseMetaTests.cs` (new)
   - `new ResponseMeta("sha256:abc")` → `Meta.OntologyVersion == "sha256:abc"`
   - Expected failure: `ResponseMeta` type does not exist

2. **[RED]** Write test: `ResponseMeta_JsonSerialization_EmitsUnderscoreMetaFriendlyForm`
   - Serialize a wrapping record `record Wrap(ResponseMeta Meta);` and assert the JSON contains property name `"OntologyVersion"` (we'll handle the wire-format `_meta` mapping at the result-record level in subsequent tasks)

3. **[GREEN]** Add `ResponseMeta` record
   - File: `src/Strategos.Ontology.MCP/ResponseMeta.cs` (new)
   - `public sealed record ResponseMeta(string OntologyVersion);`

4. **[REFACTOR]** None expected

**Dependencies:** None (record itself is standalone — wiring into results is in B6/B7/B8)
**Parallelizable:** Yes (parallel-safe with B1, B2, B3)

---

### Task B5: `OntologyToolDiscovery` populates Title + OutputSchema + Annotations per matrix
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Discover_OntologyExplore_HasReadOnlyAndIdempotentHints`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyToolDiscoveryAnnotationTests.cs` (new)
   - Build a minimal graph, run `new OntologyToolDiscovery(graph).Discover()`, find descriptor by `Name == "ontology_explore"`
   - Assert `Annotations.ReadOnlyHint == true && Annotations.IdempotentHint == true && Annotations.DestructiveHint == false && Annotations.OpenWorldHint == false`

2. **[RED]** Write test: `Discover_OntologyQuery_HasReadOnlyAndIdempotentHints` — same shape

3. **[RED]** Write test: `Discover_OntologyAction_HasDestructiveHint`
   - Assert `Annotations.DestructiveHint == true && Annotations.ReadOnlyHint == false && Annotations.IdempotentHint == false && Annotations.OpenWorldHint == false`

4. **[RED]** Write test: `Discover_AllTools_HaveNonNullTitle`
   - Iterate `Discover()` output, assert each `Title` is non-null and non-empty

5. **[RED]** Write test: `Discover_AllTools_HaveNonNullOutputSchema`
   - Iterate, assert each `OutputSchema` is non-null and `OutputSchema.Value.GetRawText()` contains `"type"`

6. **[GREEN]** Update `OntologyToolDiscovery.Discover()`
   - File: `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs`
   - For each of the three descriptors, populate `Title`, `OutputSchema = JsonSchemaHelper.JsonSchemaFor<TResult>()`, and `Annotations` per the matrix in design §5
     - `ontology_explore` → Title `"Explore Ontology Schema"`, OutputSchema for `ExploreResult`, Annotations `(true, false, true, false)`
     - `ontology_query` → Title `"Query Ontology Objects"`, OutputSchema for `QueryResult` (see open question §11.1 — for now, schema for the non-semantic shape; `oneOf` deferred to follow-up if it surfaces problems), Annotations `(true, false, true, false)`
     - `ontology_action` → Title `"Execute Ontology Action"`, OutputSchema for `ActionToolResult`, Annotations `(false, true, false, false)`

7. **[REFACTOR]** Extract a private `BuildExploreDescriptor()` / `BuildQueryDescriptor()` / `BuildActionDescriptor()` helper trio if `Discover()` becomes too long

**Dependencies:** B1, B2, B3
**Parallelizable:** No (sequential after B1/B2/B3 land)

---

### Task B6: `ExploreResult` gains `Meta` + `OntologyExploreTool` stamps it
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Explore_ResultCarriesMetaWithGraphVersion`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyExploreToolMetaTests.cs` (new)
   - Build a graph, instantiate `OntologyExploreTool(graph)`, call `Explore("domains")`, assert `result.Meta.OntologyVersion == graph.Version`
   - Expected failure: `ExploreResult` has no `Meta` property

2. **[RED]** Write test: `Explore_AllScopes_CarryMeta`
   - Loop through every scope branch (`"domains"`, `"objectTypes"`, `"actions"`, `"links"`, `"events"`, `"interfaces"`, `"workflowChains"`, `"vectorProperties"`, the unknown-scope fallback, and the traversal branch via `traverseFrom`), assert each result has `Meta.OntologyVersion == graph.Version`

3. **[GREEN]**
   - File: `src/Strategos.Ontology.MCP/ExploreResult.cs` — add `ResponseMeta Meta` to the positional record (`public sealed record ExploreResult(string Scope, IReadOnlyList<Dictionary<string, object?>> Items, ResponseMeta Meta);`). Add `[JsonPropertyName("_meta")]` attribute on the `Meta` property if/when needed for wire-format mapping (deferred to wire-test confirmation; design §11.3 open question)
   - File: `src/Strategos.Ontology.MCP/OntologyExploreTool.cs` — add a private `ResponseMeta CurrentMeta() => new(_graph.Version);` helper, replace every `return new ExploreResult(scope, items)` and `return new ExploreResult(scope, [])` with the three-arg form including `CurrentMeta()`. Eight return sites to update (one per scope branch + traversal + unknown fallback)

4. **[REFACTOR]** None expected (the helper is the cleanup)

**Dependencies:** A1 (Version property exists), B4 (ResponseMeta exists)
**Parallelizable:** No (sequential after A1 + B4)

---

### Task B7: `QueryResult` and `SemanticQueryResult` gain `Meta` + `OntologyQueryTool` stamps it
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Query_NonSemantic_ResultCarriesMetaWithGraphVersion`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyQueryToolMetaTests.cs` (new)
   - Run a basic non-semantic query, assert `result.Meta.OntologyVersion == graph.Version`

2. **[RED]** Write test: `Query_Semantic_ResultCarriesMetaWithGraphVersion`
   - Run a semantic-search query (using whatever in-test stub `IObjectSetProvider` shape `OntologyQueryTool` accepts), assert `Meta.OntologyVersion == graph.Version`

3. **[GREEN]**
   - File: `src/Strategos.Ontology.MCP/QueryResult.cs` — add `ResponseMeta Meta` to the record
   - File: `src/Strategos.Ontology.MCP/SemanticQueryResult.cs` — same
   - File: `src/Strategos.Ontology.MCP/OntologyQueryTool.cs` — wire `new ResponseMeta(_graph.Version)` into every result construction site

4. **[REFACTOR]** Extract a `CurrentMeta()` helper consistent with B6

**Dependencies:** A1, B4, B6 (for pattern consistency)
**Parallelizable:** No (sequential)

---

### Task B8: `ActionToolResult` gains `Meta` + `OntologyActionTool` stamps it
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Action_ResultCarriesMetaWithGraphVersion`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyActionToolMetaTests.cs` (new)
   - Dispatch a no-op action through `OntologyActionTool` (using the same in-test stub shape used elsewhere in the existing action-tool tests), assert `result.Meta.OntologyVersion == graph.Version`

2. **[GREEN]**
   - File: `src/Strategos.Ontology.MCP/ActionToolResult.cs` — add `ResponseMeta Meta` to the record
   - File: `src/Strategos.Ontology.MCP/OntologyActionTool.cs` — wire `new ResponseMeta(_graph.Version)` at each result construction site

3. **[REFACTOR]** Same `CurrentMeta()` helper pattern

**Dependencies:** A1, B4
**Parallelizable:** Yes (parallel-safe with B6 and B7 once dependencies land)

---

### Task B9: `OntologyServerCapabilitiesProvider`
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `GetServerCapabilities_ReturnsCurrentGraphVersion`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyServerCapabilitiesProviderTests.cs` (new — see review-fix note below)
   - Build a graph, call `new OntologyServerCapabilitiesProvider(graph).GetServerCapabilities()`, assert `result.OntologyVersion == "sha256:" + graph.Version` (provider routes through `ResponseMeta.ForGraph(...)` which prepends the wire-format prefix)
   - Expected failure: `OntologyServerCapabilitiesProvider` type doesn't exist
2. **[RED]** Write test: `OntologyServerCapabilitiesProvider_NullGraph_Throws`
   - Same file. `await Assert.That(() => new OntologyServerCapabilitiesProvider(null!)).Throws<ArgumentNullException>()`.
3. **[GREEN]**
   - File: `src/Strategos.Ontology.MCP/OntologyServerCapabilitiesProvider.cs` (new) — `public sealed class OntologyServerCapabilitiesProvider` with `ArgumentNullException.ThrowIfNull(graph)` constructor and `GetServerCapabilities() => new(ResponseMeta.ForGraph(_graph).OntologyVersion)`.
   - File: `src/Strategos.Ontology.MCP/OntologyServerCapabilities.cs` (new) — `public sealed record OntologyServerCapabilities(string OntologyVersion);`

4. **[REFACTOR]** None expected

**Review-fix note (M6, post-merge of initial PR #55 implementation):** Initial implementation placed `GetServerCapabilities()` on `OntologyToolDiscovery`. Review surfaced an SRP concern — discovery and server-capability exposure are different concerns sharing only `_graph`. Refactored into `OntologyServerCapabilitiesProvider`. The test moved from `OntologyToolDiscoveryAnnotationTests.cs` to `OntologyServerCapabilitiesProviderTests.cs`; a null-graph constructor test was added.

**Dependencies:** A1, B5 (for file proximity)
**Parallelizable:** Yes (small, isolated; can run alongside B6/B7/B8)

---

## Parallelization summary

```
Track A (sequential):
  A1 → A2 → A3 → A4 → A5

Track B parallel start:
  B1 (independent) ─┐
  B3 (independent) ─┼─→ B5 (after B1, B2, B3) ─┐
  B4 (independent) ─┘                           │
  B2 (after B1) ──────────────────────────┐    │
                                          │    │
  Wait for A1 ─────────┐                  │    │
                       │                  │    │
  Then B6, B7, B8, B9 ─┴─→ (all need A1 + B4) ─┴─→ done
```

Practical delegation shape (two worktrees):

- **Worktree 1 (Track A):** A1 → A2 → A3 → A4 → A5. Single PR. Lands first.
- **Worktree 2 (Track B):** B1 → B2 → B3 → B4 → B5 → B6 → B7 → B8 → B9 (some interleaving possible but sequential is simpler for one implementer). Single PR. Blocks on Worktree 1's merge for the meta-envelope tests (B6+) to compile.

If we run Track B parallel-from-the-start, B1–B5 can land before A1 merges; B6–B9 stub the `_graph.Version` reference behind a TODO that the integration commit fills in once A1 lands. This is more ceremony than benefit for a 14-task slice; recommend the simpler "Track A first, then Track B" sequencing.

---

## Acceptance criteria (rolled up from design §7)

- All 14 tasks complete; full test suite green for both `Strategos.Ontology.Tests` and `Strategos.Ontology.MCP.Tests`
- `OntologyGraph.Version` returns a 64-char lowercase hex string
- `Version` is stable across builds and sensitive to structural mutations per §4.1
- `Version` is INSENSITIVE to `Description` and `Warnings` mutations (regression-tested in A4)
- Reference fixture's `Version` matches a pinned constant (A5)
- `OntologyToolDescriptor` constructed via the two-arg form has all-false `Annotations`, null `Title`, null `OutputSchema`
- `OntologyToolDiscovery.Discover()` populates the annotation matrix per design §5 for the three currently-shipping tools
- Every result type (`ExploreResult`, `QueryResult`, `SemanticQueryResult`, `ActionToolResult`) carries a non-null `Meta` with `OntologyVersion` matching the graph
- `OntologyServerCapabilitiesProvider.GetServerCapabilities()` returns the current graph version in wire format (`"sha256:" + graph.Version`) (split out of `OntologyToolDiscovery` per M6 review fix)
- No existing test in either project regresses (backward-compat verified)
