# Implementation Plan: Agent-First Engine Support

**Design:** `docs/designs/2026-04-12-agent-first-engine-support.md`
**Date:** 2026-04-12
**Tasks:** 12
**Tracks:** 3 (A: Link Descriptions, B: Expression Evaluator, C: Provider Refactor)
**Parallelization:** Tracks A and B run in parallel. Track C depends on Track B.

---

## Track A: Link Descriptions

### Task 1: LinkDescriptor — Add Description property
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `LinkDescriptor_Description_DefaultsToNull`
   - File: `src/Strategos.Ontology.Tests/Descriptors/LinkDescriptorTests.cs`
   - Add test verifying `new LinkDescriptor("Orders", "TradeOrder", LinkCardinality.OneToMany).Description` is null
   - Expected failure: `LinkDescriptor` has no `Description` property

2. **[RED]** Write test: `LinkDescriptor_WithDescription_StoresValue`
   - Same file
   - Create descriptor `with { Description = "test description" }`, assert value stored
   - Expected failure: same — property doesn't exist

3. **[GREEN]** Add `Description` property to `LinkDescriptor`
   - File: `src/Strategos.Ontology/Descriptors/LinkDescriptor.cs`
   - Add `public string? Description { get; init; }`

4. **[REFACTOR]** None expected

**Dependencies:** None
**Parallelizable:** Yes (Track A)

---

### Task 2: ILinkBuilder + LinkBuilder — Description method
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `HasMany_WithDescription_SetsDescriptorDescription`
   - File: `src/Strategos.Ontology.Tests/Builder/IObjectTypeBuilderTests.cs` (extend existing)
   - Build an ontology with `obj.HasMany<TestOrder>("Orders").Description("Order link desc")`, assert `LinkDescriptor.Description == "Order link desc"`
   - Expected failure: `ILinkBuilder` has no `Description` method

2. **[RED]** Write test: `HasOne_WithDescription_SetsDescriptorDescription`
   - Same file
   - Same pattern with `HasOne`

3. **[GREEN]** Add `Description` to `ILinkBuilder` and `LinkBuilder`
   - File: `src/Strategos.Ontology/Builder/ILinkBuilder.cs` — add `ILinkBuilder Description(string description);`
   - File: `src/Strategos.Ontology/Builder/LinkBuilder.cs` — add `_description` field, `Description()` method, update `Build()` to use `with { Description = _description }`

4. **[REFACTOR]** Simplify `LinkBuilder.Build()` to use a single `with` expression for both `InverseLinkName` and `Description`

**Dependencies:** Task 1
**Parallelizable:** Yes (Track A, sequential within track)

---

### Task 3: CrossDomainLinkDescriptor + ICrossDomainLinkBuilder — Description
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `CrossDomainLinkBuilder_WithDescription_SetsDescription`
   - File: `src/Strategos.Ontology.Tests/Builder/CrossDomainLinkBuilderTests.cs` (extend existing)
   - Build with `.Description("Cross-domain desc")`, assert `descriptor.Description == "Cross-domain desc"`
   - Expected failure: `ICrossDomainLinkBuilder` has no `Description` method

2. **[RED]** Write test: `CrossDomainLinkDescriptor_Description_DefaultsToNull`
   - File: `src/Strategos.Ontology.Tests/Builder/CrossDomainLinkBuilderTests.cs`
   - Build without `.Description()`, assert `descriptor.Description` is null

3. **[GREEN]** Add `Description` to descriptor, interface, and builder
   - File: `src/Strategos.Ontology/Descriptors/CrossDomainLinkDescriptor.cs` — add `public string? Description { get; init; }`
   - File: `src/Strategos.Ontology/Builder/ICrossDomainLinkBuilder.cs` — add `ICrossDomainLinkBuilder Description(string description);`
   - File: `src/Strategos.Ontology/Builder/CrossDomainLinkBuilder.cs` — add `_description` field, `Description()` method, update `Build()` to set `Description = _description`

4. **[REFACTOR]** None expected

**Dependencies:** None
**Parallelizable:** Yes (Track A, can run parallel with Tasks 1-2)

---

### Task 4: ResolvedCrossDomainLink + OntologyGraphBuilder — Thread Description
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `CrossDomainLink_Description_ThreadedToResolved`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphBuilderTests.cs` (extend existing)
   - Build a graph with a cross-domain link that has `.Description("test desc")`. After graph build, check `graph.CrossDomainLinks[0].Description == "test desc"`
   - Expected failure: `ResolvedCrossDomainLink` constructor doesn't accept `Description`

2. **[GREEN]** Add `Description` to `ResolvedCrossDomainLink` and thread through `OntologyGraphBuilder.ResolveCrossDomainLinks`
   - File: `src/Strategos.Ontology/ResolvedCrossDomainLink.cs` — add `string? Description = null` positional parameter
   - File: `src/Strategos.Ontology/OntologyGraphBuilder.cs` — in `ResolveCrossDomainLinks`, add `Description: descriptor.Description` to the `new ResolvedCrossDomainLink(...)` call (~line 160-167)

3. **[REFACTOR]** None expected

**Dependencies:** Task 3
**Parallelizable:** Yes (Track A, sequential after Task 3)

---

### Task 5: LinkTraversalResult — Thread Description from LinkDescriptor
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `TraverseLinks_ResultIncludesLinkDescription`
   - File: `src/Strategos.Ontology.Tests/OntologyGraphTraversalTests.cs` (extend existing)
   - Build a graph where a link has a Description. Call `graph.TraverseLinks(...)`, assert `result[0].Description` matches the link's description
   - Expected failure: `LinkTraversalResult` has no `Description` property

2. **[GREEN]** Add `Description` to `LinkTraversalResult` and thread through `OntologyGraph.TraverseLinks`
   - File: `src/Strategos.Ontology/LinkTraversalResult.cs` — add `string? Description = null` positional parameter
   - File: `src/Strategos.Ontology/OntologyGraph.cs` — in `TraverseLinks` (~line 106), add `link.Description` to `new LinkTraversalResult(targetType, link.Name, currentDepth + 1, link.Description)`

3. **[REFACTOR]** None expected

**Dependencies:** Task 1 (needs `LinkDescriptor.Description`)
**Parallelizable:** Yes (Track A, sequential after Task 1)

---

### Task 6: OntologyExploreTool — Surface link descriptions
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `OntologyExplore_Links_IncludesDescription`
   - File: `src/Strategos.Ontology.MCP.Tests/OntologyExploreToolTests.cs` (extend existing)
   - Build a graph where a link has `.Description(...)`. Explore with `scope: "links"`. Assert result dictionary contains `["description"]` key with correct value
   - Expected failure: `ExploreLinks` doesn't include `description` key

2. **[RED]** Write test: `OntologyExplore_Traversal_IncludesDescription`
   - Same file
   - Test traversal result includes `["description"]` key
   - Expected failure: `ExploreTraversal` doesn't include `description` key

3. **[GREEN]** Add description to explore tool output
   - File: `src/Strategos.Ontology.MCP/OntologyExploreTool.cs`
     - `ExploreLinks` (~line 106): add `["description"] = l.Description` to dictionary
     - `ExploreTraversal` (~line 164): add `["description"] = r.Description` to dictionary

4. **[GREEN]** Update `TestTradingDomainOntology` to include a link description for test coverage
   - File: `src/Strategos.Ontology.MCP.Tests/TestOntologyGraphFactory.cs`
   - Add `.Description("Orders placed against this position")` to the `HasMany<TestOrder>("Orders")` call

5. **[REFACTOR]** None expected

**Dependencies:** Tasks 1, 2, 5 (needs link descriptions on descriptors and traversal results)
**Parallelizable:** Yes (Track A, final task in track)

---

## Track B: InMemoryExpressionEvaluator

### Task 7: Evaluator — Constructor + Root + Filter + Include expressions
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test fixture: Create `EvaluatorTestDomain` with linked types
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryExpressionEvaluatorTests.cs` (**NEW**)
   - Test domain types: `EvalSource(string Name, int Value)`, `EvalTarget(string Label)`, `IEvalInterface` (CLR interface implemented by `EvalSource`)
   - Test domain ontology: registers `EvalSource` with `HasMany<EvalTarget>("targets")` link, implements `IEvalInterface`
   - Helper: `BuildTestGraph()` returns `OntologyGraph`, `BuildTestResolver()` returns `Func<string, IReadOnlyList<object>>`

2. **[RED]** Write test: `Evaluate_RootExpression_ReturnsAllItems`
   - Assert evaluator returns all items from the resolver for the descriptor name
   - Expected failure: `InMemoryExpressionEvaluator` class does not exist

3. **[RED]** Write test: `Evaluate_FilterExpression_AppliesPredicate`
   - Build `RootExpression` → `FilterExpression(x => x.Value > 5)`, assert filtered correctly

4. **[RED]** Write test: `Evaluate_FilterChain_AppliesAllPredicates`
   - Chain two `.Where()` filters, assert both applied

5. **[RED]** Write test: `Evaluate_IncludeExpression_PassesThrough`
   - Wrap expression in `IncludeExpression`, assert same results as without

6. **[RED]** Write test: `Evaluate_EmptyItemResolver_ReturnsEmpty`
   - Resolver returns empty list, assert empty result

7. **[GREEN]** Implement `InMemoryExpressionEvaluator` with Root + Filter + Include
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs` (**NEW**)
   - Constructor takes `OntologyGraph`, builds `_descriptorIndex`
   - `Evaluate<T>` method with recursive expression matching
   - Handle: `RootExpression`, `FilterExpression`, `IncludeExpression`
   - Unknown expressions: `throw new NotSupportedException`

8. **[REFACTOR]** Extract shared `ResolveSourceDescriptorName` helper method

**Dependencies:** None
**Parallelizable:** Yes (Track B)

---

### Task 8: Evaluator — TraverseLinkExpression (schema-level)
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Evaluate_TraverseLink_ReturnsTargetTypeItems`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryExpressionEvaluatorTests.cs`
   - Build `RootExpression("EvalSource")` → `TraverseLinkExpression("targets", typeof(EvalTarget))`, assert returns all `EvalTarget` items
   - Expected failure: `TraverseLinkExpression` falls through to `NotSupportedException`

2. **[RED]** Write test: `Evaluate_TraverseLink_ThenFilter_FiltersTargetItems`
   - Chain: `Root("EvalSource")` → `TraverseLink("targets")` → `Filter(t => t.Label == "A")`
   - Assert: only matching `EvalTarget` items returned

3. **[RED]** Write test: `Evaluate_TraverseLink_UnknownLink_Throws`
   - Use link name "nonexistent", assert throws `InvalidOperationException` with descriptive message

4. **[GREEN]** Add `TraverseLinkExpression` handling
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs`
   - Implement `ResolveSourceDescriptorName` to walk source subtree
   - Look up link descriptor from `_descriptorIndex[sourceDescriptorName].Links`
   - Call `itemResolver(link.TargetTypeName)` and cast to `T`

5. **[REFACTOR]** None expected

**Dependencies:** Task 7
**Parallelizable:** No (sequential within Track B)

---

### Task 9: Evaluator — InterfaceNarrowExpression
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Evaluate_InterfaceNarrow_FiltersToImplementors`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryExpressionEvaluatorTests.cs`
   - Seed both `EvalSource` and `EvalTarget` items. Build expression: `Root("EvalSource")` → `InterfaceNarrowExpression(typeof(IEvalInterface))`
   - Assert: only `EvalSource` items returned (since only `EvalSource` implements `IEvalInterface`)
   - Expected failure: `InterfaceNarrowExpression` falls through to `NotSupportedException`

2. **[RED]** Write test: `Evaluate_InterfaceNarrow_NoImplementors_ReturnsEmpty`
   - Build expression narrowing to an interface that no seeded items implement
   - Assert: empty result

3. **[GREEN]** Add `InterfaceNarrowExpression` handling
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs`
   - Recursively evaluate source, then filter by `typeof(T).IsAssignableFrom(item.GetType())`

4. **[REFACTOR]** None expected

**Dependencies:** Task 7
**Parallelizable:** No (sequential within Track B, can run after Task 7 but ordered after Task 8 for cleaner diffs)

---

### Task 10: Evaluator — RawFilterExpression + Error handling + Thread safety
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `Evaluate_RawFilter_ThrowsNotSupported`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryExpressionEvaluatorTests.cs`
   - Build `RawFilterExpression` wrapper, assert throws `NotSupportedException`
   - Expected failure: `RawFilterExpression` may fall through to the generic `NotSupportedException` — test should verify the message is descriptive

2. **[RED]** Write test: `Evaluate_UnknownDescriptor_ReturnsEmpty`
   - Resolver returns empty list for unknown descriptor. Assert evaluator returns empty without throwing
   - (Note: the item resolver returning empty is the normal case for unknown descriptors; the evaluator doesn't validate descriptor existence against the graph for Root/Filter paths — only for TraverseLink)

3. **[RED]** Write test: `Evaluate_ConcurrentCalls_ThreadSafe`
   - Launch 10 parallel `Evaluate` calls, assert all return correct results without exceptions

4. **[GREEN]** Ensure `RawFilterExpression` throws with descriptive message. Verify error paths are clean.
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs`
   - Add explicit `RawFilterExpression` case in the match with descriptive `NotSupportedException`
   - Verify the generic fallback `NotSupportedException` for truly unknown expression types

5. **[REFACTOR]** Review evaluator for any shared logic that can be consolidated

**Dependencies:** Tasks 7-9
**Parallelizable:** No (sequential within Track B, final task)

---

## Track C: InMemoryObjectSetProvider Refactor

### Task 11: Provider — Graph-aware constructors + ExecuteAsync/StreamAsync delegation
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ExecuteAsync_WithGraph_TraverseLink_Works`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryObjectSetProviderTests.cs` (extend existing)
   - Create provider with `new InMemoryObjectSetProvider(graph)`, seed items, execute a `TraverseLinkExpression`
   - Assert: correct target items returned
   - Expected failure: constructor `InMemoryObjectSetProvider(OntologyGraph)` does not exist

2. **[RED]** Write test: `ExecuteAsync_WithGraph_InterfaceNarrow_Works`
   - Same pattern with `InterfaceNarrowExpression`

3. **[RED]** Write test: `ExecuteAsync_WithoutGraph_LegacyBehavior`
   - Use existing parameterless constructor, verify `FilterExpression` still works identically
   - Expected: passes (backward compat — this is a safety net, not a failing test)

4. **[RED]** Write test: `StreamAsync_WithGraph_DelegatesToEvaluator`
   - Create graph-aware provider, stream with a `TraverseLinkExpression`, collect results
   - Assert: correct items streamed

5. **[GREEN]** Add graph-aware constructors and delegation
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`
   - Add `_evaluator` field (`InMemoryExpressionEvaluator?`)
   - Add `InMemoryObjectSetProvider(OntologyGraph graph)` constructor
   - Add `InMemoryObjectSetProvider(OntologyGraph graph, IEmbeddingProvider? embeddingProvider)` constructor
   - Add `GetSeededItems(string descriptorName)` overload returning `IReadOnlyList<object>`
   - Modify `ExecuteAsync`: if `_evaluator != null`, delegate; else legacy path
   - Modify `StreamAsync`: if `_evaluator != null`, delegate; else legacy path
   - Rename existing `ApplyExpression` to `ApplyExpressionLegacy`

6. **[REFACTOR]** Consider whether `ApplyExpressionLegacy` can be removed if all tests pass through the evaluator path

**Dependencies:** Tasks 7-10 (needs evaluator)
**Parallelizable:** No (Track C, depends on Track B)

---

### Task 12: Provider — ExecuteSimilarityAsync source filtering + backward compat
**Phase:** RED → GREEN → REFACTOR

1. **[RED]** Write test: `ExecuteSimilarityAsync_WithGraph_SourceFiltered`
   - File: `src/Strategos.Ontology.Tests/ObjectSets/InMemoryObjectSetProviderTests.cs`
   - Create graph-aware provider, seed items, execute similarity with a `FilterExpression` on the source
   - Assert: evaluator filters source items before keyword scoring

2. **[RED]** Write test: `ExecuteSimilarityAsync_WithoutGraph_LegacyBehavior`
   - Same test with parameterless constructor, verify existing behavior unchanged

3. **[GREEN]** Wire evaluator into `ExecuteSimilarityAsync`
   - File: `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs`
   - In `ExecuteSimilarityAsync`: if `_evaluator != null`, use `_evaluator.Evaluate<T>(expression.Source, GetSeededItems)` for source filtering; else use `ApplyExpressionLegacy`

4. **[REFACTOR]** Clean up any duplication between evaluator and legacy paths

**Dependencies:** Task 11
**Parallelizable:** No (Track C, sequential after Task 11)

---

## Dependency Graph

```
Track A (Link Descriptions):        Track B (Expression Evaluator):
  Task 1 ─┬─ Task 2                   Task 7
           │     │                       │
           │     │                     Task 8
  Task 3   │     │                       │
    │      │     │                     Task 9
  Task 4   Task 5                        │
    │         │                        Task 10
    └────┬────┘                          │
         │                               │
       Task 6                      Track C (Provider Refactor):
                                     Task 11
                                       │
                                     Task 12
```

## Parallelization Summary

| Group | Tasks | Can run in parallel with |
|-------|-------|-------------------------|
| Track A | 1 → 2, 3 → 4, then 5, then 6 | Track B |
| Track B | 7 → 8 → 9 → 10 | Track A |
| Track C | 11 → 12 | Nothing (depends on Track B completion) |

**Worktree strategy:**
- Track A: single worktree, tasks run sequentially
- Track B: single worktree, tasks run sequentially
- Track C: single worktree after Track B merges, tasks run sequentially

## Test File Summary

| Test File | Tasks | Status |
|-----------|-------|--------|
| `src/Strategos.Ontology.Tests/Descriptors/LinkDescriptorTests.cs` | 1 | Extend |
| `src/Strategos.Ontology.Tests/Builder/IObjectTypeBuilderTests.cs` | 2 | Extend |
| `src/Strategos.Ontology.Tests/Builder/CrossDomainLinkBuilderTests.cs` | 3 | Extend |
| `src/Strategos.Ontology.Tests/OntologyGraphBuilderTests.cs` | 4 | Extend |
| `src/Strategos.Ontology.Tests/OntologyGraphTraversalTests.cs` | 5 | Extend |
| `src/Strategos.Ontology.MCP.Tests/OntologyExploreToolTests.cs` | 6 | Extend |
| `src/Strategos.Ontology.MCP.Tests/TestOntologyGraphFactory.cs` | 6 | Extend |
| `src/Strategos.Ontology.Tests/ObjectSets/InMemoryExpressionEvaluatorTests.cs` | 7-10 | **NEW** |
| `src/Strategos.Ontology.Tests/ObjectSets/InMemoryObjectSetProviderTests.cs` | 11-12 | Extend |

## Production File Summary

| Production File | Tasks | Change |
|-----------------|-------|--------|
| `src/Strategos.Ontology/Descriptors/LinkDescriptor.cs` | 1 | Add property |
| `src/Strategos.Ontology/Builder/ILinkBuilder.cs` | 2 | Add method |
| `src/Strategos.Ontology/Builder/LinkBuilder.cs` | 2 | Implement method |
| `src/Strategos.Ontology/Descriptors/CrossDomainLinkDescriptor.cs` | 3 | Add property |
| `src/Strategos.Ontology/Builder/ICrossDomainLinkBuilder.cs` | 3 | Add method |
| `src/Strategos.Ontology/Builder/CrossDomainLinkBuilder.cs` | 3 | Implement method |
| `src/Strategos.Ontology/ResolvedCrossDomainLink.cs` | 4 | Add parameter |
| `src/Strategos.Ontology/OntologyGraphBuilder.cs` | 4 | Thread description |
| `src/Strategos.Ontology/LinkTraversalResult.cs` | 5 | Add parameter |
| `src/Strategos.Ontology/OntologyGraph.cs` | 5 | Thread description |
| `src/Strategos.Ontology.MCP/OntologyExploreTool.cs` | 6 | Add description to output |
| `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs` | 7-10 | **NEW** |
| `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs` | 11-12 | Refactor |
