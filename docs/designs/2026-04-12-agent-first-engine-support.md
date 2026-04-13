# Agent-First Engine Support — Strategos Extensions

**Date:** 2026-04-12
**Depends on:** Strategos.Ontology v2.4.1 (shipped), Agent-First Engine design (`../valkyrie/docs/designs/2026-04-12-agent-first-engine.md`)
**Scope:** Extensions to `Strategos.Ontology` and `Strategos.Ontology.MCP` that enable external consumers (Valkyrie, test harnesses, embedded scenarios) to evaluate `ObjectSetExpression` trees against in-memory data from arbitrary sources — not just the built-in `InMemoryObjectSetProvider`'s internal dictionary.

---

## 1. Context

### 1.1 The consumer need

The Valkyrie Agent-First Engine design specifies three Strategos provider implementations that live in Valkyrie's Agent assembly:

- `GameLoopObjectSetProvider : IObjectSetProvider` — reads from `IFrameStateProvider.Current` (frame state snapshots)
- `CommandQueueActionDispatcher : IActionDispatcher` — enqueues to `CommandIntakeQueue`
- `FrameStateEventStreamProvider : IEventStreamProvider` — reads from `FrameState.RecentEvents`

The dispatcher and event stream provider are straightforward interface implementations — Strategos provides clean contracts, Valkyrie implements them. No Strategos changes needed.

The `GameLoopObjectSetProvider` is different. It must evaluate `ObjectSetExpression` trees — handling `FilterExpression`, `TraverseLinkExpression`, `InterfaceNarrowExpression`, and `IncludeExpression` — against items that come from the game loop, not from a database or an internal dictionary. The expression evaluation logic is non-trivial and already exists in Strategos, but it's locked inside `InMemoryObjectSetProvider` as a `private static` method.

### 1.2 The current state

`InMemoryObjectSetProvider` (367 lines) handles expression evaluation in `ApplyExpression` (line 302):

```csharp
private static List<T> ApplyExpression<T>(List<T> items, ObjectSetExpression expression)
{
    if (expression is FilterExpression filter) { /* compile + apply */ }
    if (expression is IncludeExpression include) { /* pass-through */ }
    return items; // RootExpression or unhandled — return all
}
```

**Three expression types silently fall through unhandled:**

| Expression | Current Behavior | Impact |
|---|---|---|
| `TraverseLinkExpression` | Returns all items unfiltered | Link traversal queries return wrong results |
| `InterfaceNarrowExpression` | Returns all items unfiltered | Interface narrowing has no effect |
| `RawFilterExpression` | Returns all items unfiltered | MCP `filter` parameter is non-functional |

The provider also has **no reference to `OntologyGraph`**, so even adding expression handling code wouldn't enable link resolution or interface lookup.

Additionally, `LinkDescriptor` and all cross-domain link types lack a `Description` field. The Agent-First Engine design relies heavily on link descriptions as design-intent metadata for agent reasoning (e.g., "BoxFormation's perimeter coverage neutralizes wedge's concentrated advance").

### 1.3 What this design delivers

Three components, each independently valuable:

1. **Link descriptions** — `Description` property on link descriptors and builder methods. Enables agents to understand *why* design relationships exist, not just *that* they exist.
2. **`InMemoryExpressionEvaluator`** — A public, graph-aware, reusable class that evaluates `ObjectSetExpression` trees against items from any source. Extracted from and generalizing `InMemoryObjectSetProvider`'s logic.
3. **`InMemoryObjectSetProvider` refactor** — Delegates to the evaluator internally, gaining TraverseLink and InterfaceNarrow support for free.

---

## 2. Scope

### In scope

- Add `Description` to `LinkDescriptor`, `CrossDomainLinkDescriptor`, `ResolvedCrossDomainLink`
- Add `Description()` to `ILinkBuilder`, `ICrossDomainLinkBuilder` and their implementations
- Surface link descriptions in `OntologyExploreTool`
- New public `InMemoryExpressionEvaluator` class in `Strategos.Ontology.ObjectSets`
- Handle `TraverseLinkExpression` (schema-level traversal)
- Handle `InterfaceNarrowExpression` (CLR-level type filtering)
- Handle `RawFilterExpression` → `NotSupportedException` (explicit, documented)
- Refactor `InMemoryObjectSetProvider` to use the evaluator

### Out of scope

- New NuGet packages — all changes live in `Strategos.Ontology` and `Strategos.Ontology.MCP`
- `QueryContext` / snapshot metadata — purely a consumer implementation detail
- Instance-level link traversal (following materialized link instances between specific objects) — deferred; schema-level traversal is sufficient for initial use cases
- `RawFilterExpression` parsing and evaluation — deferred; the evaluator throws `NotSupportedException`, matching `ExpressionTranslator`'s behavior for unsupported types
- Valkyrie-side provider implementations — those live in `valkyrie`
- `IActionDispatcher` or `IEventStreamProvider` changes — contracts are sufficient
- `IObjectSetWriter` changes — non-persistent semantics already implemented by `InMemoryObjectSetProvider`

---

## 3. Component 1: Link Descriptions

### 3.1 Descriptor changes

**`LinkDescriptor`** — add optional `Description`:

```csharp
public sealed record LinkDescriptor(
    string Name,
    string TargetTypeName,
    LinkCardinality Cardinality,
    IReadOnlyList<PropertyDescriptor>? EdgeProperties = null)
{
    public IReadOnlyList<PropertyDescriptor> EdgeProperties { get; init; } =
        EdgeProperties ?? [];

    public string? InverseLinkName { get; init; }

    // NEW
    public string? Description { get; init; }
}
```

**`CrossDomainLinkDescriptor`** — add optional `Description`:

```csharp
public sealed record CrossDomainLinkDescriptor(
    string Name,
    Type SourceType,
    string TargetDomain,
    string TargetTypeName,
    LinkCardinality Cardinality)
{
    public IReadOnlyList<PropertyDescriptor> EdgeProperties { get; init; } = [];

    // NEW
    public string? Description { get; init; }
}
```

**`ResolvedCrossDomainLink`** — add optional `Description`:

```csharp
public sealed record ResolvedCrossDomainLink(
    string Name,
    string SourceDomain,
    ObjectTypeDescriptor SourceObjectType,
    string TargetDomain,
    ObjectTypeDescriptor TargetObjectType,
    LinkCardinality Cardinality,
    IReadOnlyList<PropertyDescriptor> EdgeProperties,
    string? Description = null);  // NEW — positional with default
```

### 3.2 Builder changes

**`ILinkBuilder`** — add `Description`:

```csharp
public interface ILinkBuilder
{
    ILinkBuilder Inverse(string inverseLinkName);
    ILinkBuilder Description(string description);  // NEW
}
```

**`LinkBuilder`** (internal) — implement `Description`:

```csharp
internal sealed class LinkBuilder(LinkDescriptor baseDescriptor) : ILinkBuilder
{
    private string? _inverseLinkName;
    private string? _description;  // NEW

    public ILinkBuilder Inverse(string inverseLinkName)
    {
        _inverseLinkName = inverseLinkName;
        return this;
    }

    // NEW
    public ILinkBuilder Description(string description)
    {
        _description = description;
        return this;
    }

    public LinkDescriptor Build()
    {
        var descriptor = baseDescriptor;
        if (_inverseLinkName is not null)
            descriptor = descriptor with { InverseLinkName = _inverseLinkName };
        if (_description is not null)
            descriptor = descriptor with { Description = _description };
        return descriptor;
    }
}
```

**`ICrossDomainLinkBuilder`** — add `Description`:

```csharp
public interface ICrossDomainLinkBuilder
{
    ICrossDomainLinkBuilder From<T>();
    ICrossDomainLinkBuilder ToExternal(string domain, string typeName);
    ICrossDomainLinkBuilder ManyToMany();
    ICrossDomainLinkBuilder WithEdge(Action<IEdgeBuilder> configure);
    ICrossDomainLinkBuilder Description(string description);  // NEW
}
```

**`CrossDomainLinkBuilder`** (internal) — implement `Description`:

```csharp
// Add field:
private string? _description;

// Add method:
public ICrossDomainLinkBuilder Description(string description)
{
    _description = description;
    return this;
}

// Update Build():
public CrossDomainLinkDescriptor Build() =>
    new(name, _sourceType, _targetDomain, _targetTypeName, _cardinality)
    {
        EdgeProperties = _edgeProperties,
        Description = _description,
    };
```

**`OntologyGraphBuilder`** — thread `Description` through to `ResolvedCrossDomainLink` during resolution.

### 3.3 MCP explore tool changes

**`OntologyExploreTool.ExploreLinks`** — include description in output:

```csharp
// Line 106-113: Add description to link dictionary
var items = type.Links.Select(l => new Dictionary<string, object?>
{
    ["name"] = l.Name,
    ["targetTypeName"] = l.TargetTypeName,
    ["cardinality"] = l.Cardinality.ToString(),
    ["edgePropertyCount"] = l.EdgeProperties.Count,
    ["description"] = l.Description,  // NEW — null omitted by caller convention
}).ToList();
```

**`OntologyExploreTool.ExploreTraversal`** — include description:

```csharp
// Line 164-170: Add description to traversal result
var items = results.Select(r => new Dictionary<string, object?>
{
    ["objectType"] = r.ObjectType.Name,
    ["linkName"] = r.LinkName,
    ["depth"] = r.Depth,
    ["description"] = r.Description,  // NEW — requires LinkTraversalResult change
}).ToList();
```

**Note:** `LinkTraversalResult` (returned by `OntologyGraph.TraverseLinks`) will need a `Description` property threaded from the `LinkDescriptor`. This is a minor graph-internal change.

### 3.4 Consumer usage

After these changes, Valkyrie's ontology definition can express design intent on links:

```csharp
builder.Object<WedgeFormation>(obj =>
{
    obj.HasMany<BoxFormation>("countered_by")
        .Description("BoxFormation's perimeter coverage neutralizes wedge's concentrated advance");

    obj.HasMany<SliceCornerExecutor>("synergizes_with")
        .Description("Wedge's point unit naturally leads corner-clearing maneuvers");
});

builder.CrossDomainLink("enables")
    .From<WedgeFormation>()
    .ToExternal("behaviors", "SliceCorner")
    .Description("Wedge point position enables corner-clearing entry");
```

An agent calling `ontology_explore { scope: "links", domain: "formations", objectType: "WedgeFormation" }` now sees *why* each relationship exists.

---

## 4. Component 2: `InMemoryExpressionEvaluator`

### 4.1 API surface

```csharp
namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// Evaluates <see cref="ObjectSetExpression"/> trees against in-memory item collections
/// resolved from an external source. Graph-aware: resolves link traversals and interface
/// narrowing against the frozen <see cref="OntologyGraph"/>.
/// </summary>
/// <remarks>
/// This evaluator handles structural expressions (Filter, TraverseLink, InterfaceNarrow,
/// Include, Root). It does NOT handle <see cref="SimilarityExpression"/> (provider-specific
/// scoring) or <see cref="RawFilterExpression"/> (throws <see cref="NotSupportedException"/>).
/// <para>
/// Link traversal is schema-level: <c>TraverseLink("countered_by")</c> resolves the link
/// descriptor from the graph and returns all items of the target type. It does not follow
/// materialized link instances between specific objects.
/// </para>
/// </remarks>
public sealed class InMemoryExpressionEvaluator
{
    /// <summary>
    /// Initializes a new instance with the specified ontology graph.
    /// </summary>
    /// <param name="graph">
    /// The frozen ontology graph used to resolve link descriptors and interface implementors.
    /// </param>
    public InMemoryExpressionEvaluator(OntologyGraph graph);

    /// <summary>
    /// Evaluates an expression tree against items resolved from the given source.
    /// </summary>
    /// <typeparam name="T">The expected result element type.</typeparam>
    /// <param name="expression">The expression tree to evaluate.</param>
    /// <param name="itemResolver">
    /// Resolves items by ontology descriptor name. Called with a descriptor name (e.g.,
    /// "WedgeFormation"), returns all items of that type as an untyped list. The evaluator
    /// handles casting and filtering.
    /// </param>
    /// <returns>Filtered, traversed, or narrowed items matching the expression.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown for <see cref="RawFilterExpression"/> (string filter parsing not implemented).
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a link name or interface cannot be resolved from the ontology graph.
    /// </exception>
    public List<T> Evaluate<T>(
        ObjectSetExpression expression,
        Func<string, IReadOnlyList<object>> itemResolver) where T : class;
}
```

### 4.2 Expression handling

The evaluator processes expressions recursively, matching the tree structure built by `ObjectSet<T>`'s fluent methods:

#### `RootExpression` (base case)

```
Items ← itemResolver(expression.RootObjectTypeName)
Return Items.Cast<T>()
```

No filtering. The descriptor name routes to the correct data partition.

#### `FilterExpression` (recursive)

```
Items ← Evaluate<T>(filter.Source, itemResolver)    // recurse
Predicate ← filter.Predicate.Compile() as Func<T, bool>
Return Items.Where(Predicate)
```

Compiles the `LambdaExpression` to a delegate and applies it. Throws `InvalidOperationException` if the compiled predicate type doesn't match `Func<T, bool>` (same behavior as current `InMemoryObjectSetProvider`).

#### `IncludeExpression` (pass-through)

```
Return Evaluate<T>(include.Source, itemResolver)    // recurse
```

`IncludeExpression` is a metadata hint for the provider (which facets to include in the result). The evaluator passes through to the source — include semantics are the provider's responsibility.

#### `TraverseLinkExpression` (schema-level)

```
SourceDescriptorName ← ResolveSourceDescriptorName(traverse.Source)
SourceDescriptor ← _descriptorIndex[SourceDescriptorName]
Link ← SourceDescriptor.Links.First(l => l.Name == traverse.LinkName)
    ?? throw InvalidOperationException("Link '{traverse.LinkName}' not found on '{SourceDescriptorName}'")
TargetItems ← itemResolver(Link.TargetTypeName)
Return TargetItems.Cast<T>()
```

**Key behavior:** This is *schema-level* traversal. The evaluator does not evaluate the source expression to get source items — it only uses the source to determine which object type's links to consult. The result is all items of the target type.

**Rationale:** Instance-level traversal (following specific link instances between objects) requires a link-instance store that doesn't exist in the in-memory model. Schema-level traversal answers the question "what types does this type link to?" which is the primary use case for design-intent queries. For Valkyrie's configuration objects (formations, behaviors), this is correct behavior — `WedgeFormation.countered_by` points to `BoxFormation` as a type relationship, not as instance-to-instance links.

**`ResolveSourceDescriptorName` helper:**

Walks the source expression to find the originating descriptor name. Unlike `RootObjectTypeName` (which on `TraverseLinkExpression` returns the *target* type name), this walks the source subtree:

```csharp
private static string ResolveSourceDescriptorName(ObjectSetExpression expression) =>
    expression switch
    {
        RootExpression root => root.ObjectTypeName,
        FilterExpression filter => ResolveSourceDescriptorName(filter.Source),
        IncludeExpression include => ResolveSourceDescriptorName(include.Source),
        TraverseLinkExpression traverse => ResolveSourceDescriptorName(traverse.Source),
        InterfaceNarrowExpression narrow => ResolveSourceDescriptorName(narrow.Source),
        RawFilterExpression raw => ResolveSourceDescriptorName(raw.Source),
        _ => throw new NotSupportedException($"Cannot resolve source descriptor from {expression.GetType().Name}"),
    };
```

#### `InterfaceNarrowExpression` (CLR-level)

```
Items ← Evaluate<object>(narrow.Source, itemResolver)  // recurse with object
Return Items.Where(item => typeof(T).IsAssignableFrom(item.GetType()))
            .Cast<T>()
```

**Key behavior:** Uses CLR type assignability, not ontology interface registration. This is correct because ontology interfaces map to CLR interfaces — if a CLR type implements the interface, its items pass the filter.

**Alternative considered:** Resolving implementors from `OntologyGraph.GetImplementors()` and collecting items from each. This would work for ontology-only interfaces but would miss CLR types not registered in the ontology. CLR-level filtering is more general and simpler.

#### `RawFilterExpression`

```
throw new NotSupportedException(
    "RawFilterExpression evaluation is not supported by InMemoryExpressionEvaluator. " +
    "Use ObjectSet<T>.Where() with typed predicates instead of raw filter strings.");
```

Matches `ExpressionTranslator`'s behavior. The MCP `ontology_query` tool's `filter` parameter generates `RawFilterExpression`; providers that can parse raw filters may support it in the future.

#### `SimilarityExpression`

Not handled by the evaluator — similarity scoring is provider-specific (depends on embeddings, searchable content, scoring algorithm). The evaluator is only called for structural expression evaluation. Providers handle similarity by:
1. Calling the evaluator on `SimilarityExpression.Source` to get filtered items
2. Applying their own scoring logic

### 4.3 Internal index

The evaluator builds a `Dictionary<string, ObjectTypeDescriptor>` from `OntologyGraph.ObjectTypes` in its constructor for O(1) descriptor lookup by name. This avoids repeated linear scans during recursive evaluation.

```csharp
private readonly OntologyGraph _graph;
private readonly Dictionary<string, ObjectTypeDescriptor> _descriptorIndex;

public InMemoryExpressionEvaluator(OntologyGraph graph)
{
    _graph = graph;
    _descriptorIndex = graph.ObjectTypes.ToDictionary(t => t.Name);
}
```

### 4.4 Error handling

| Condition | Exception | Message |
|---|---|---|
| Link name not found on source type | `InvalidOperationException` | `"Link '{linkName}' not found on object type '{descriptorName}'. Available links: {names}"` |
| Source descriptor not found in graph | `InvalidOperationException` | `"Object type '{name}' not found in ontology graph. Available types: {names}"` |
| Filter predicate type mismatch | `InvalidOperationException` | `"Filter predicate type '{type}' is not compatible with Func<{T}, bool>"` |
| Raw filter expression | `NotSupportedException` | See section 4.2 |
| Unknown expression subtype | `NotSupportedException` | `"Expression type '{type}' is not supported"` |

### 4.5 Thread safety

The evaluator is stateless after construction (the internal index is read-only). It is safe to call `Evaluate` concurrently from multiple threads. The `itemResolver` delegate's thread safety is the caller's responsibility — Valkyrie's resolver reads from `IFrameStateProvider.Current` which is a volatile read by design.

---

## 5. Component 3: `InMemoryObjectSetProvider` Refactor

### 5.1 New constructor overload

```csharp
public sealed class InMemoryObjectSetProvider : IObjectSetProvider, IObjectSetWriter
{
    private readonly InMemoryExpressionEvaluator? _evaluator;

    // Existing constructors — unchanged behavior
    public InMemoryObjectSetProvider() { }
    public InMemoryObjectSetProvider(IEmbeddingProvider? embeddingProvider) { ... }

    // NEW — graph-aware constructor
    public InMemoryObjectSetProvider(OntologyGraph graph)
    {
        _evaluator = new InMemoryExpressionEvaluator(graph);
    }

    // NEW — graph-aware with embeddings
    public InMemoryObjectSetProvider(OntologyGraph graph, IEmbeddingProvider? embeddingProvider)
    {
        _evaluator = new InMemoryExpressionEvaluator(graph);
        _embeddingProvider = embeddingProvider;
    }
}
```

### 5.2 Delegation

`ExecuteAsync` and `StreamAsync` delegate to the evaluator when available:

```csharp
public Task<ObjectSetResult<T>> ExecuteAsync<T>(
    ObjectSetExpression expression, CancellationToken ct) where T : class
{
    var items = _evaluator is not null
        ? _evaluator.Evaluate<T>(expression, GetSeededItems)
        : ApplyExpressionLegacy(GetSeededItems<T>(expression.RootObjectTypeName), expression);

    return Task.FromResult(new ObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties));
}

// Item resolver delegate for the evaluator
private IReadOnlyList<object> GetSeededItems(string descriptorName)
{
    return _items.TryGetValue(descriptorName, out var items) ? items : [];
}
```

`ExecuteSimilarityAsync` uses the evaluator for source filtering, then applies its own scoring:

```csharp
public async Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
    SimilarityExpression expression, CancellationToken ct) where T : class
{
    var partitionKey = expression.RootObjectTypeName;

    // Use evaluator for source filtering (if available), then score
    var items = _evaluator is not null
        ? _evaluator.Evaluate<T>(expression.Source, GetSeededItems)
        : ApplyExpressionLegacy(GetSeededItems<T>(partitionKey), expression.Source);

    // ... existing scoring logic unchanged ...
}
```

### 5.3 Backward compatibility

- The parameterless constructor (`new InMemoryObjectSetProvider()`) continues to work identically. No graph, no evaluator, legacy `ApplyExpression` path. Existing tests pass unchanged.
- The `IEmbeddingProvider` constructor continues to work identically.
- New graph-aware constructors opt in to full expression evaluation.
- The legacy `ApplyExpression` method is renamed to `ApplyExpressionLegacy` and kept as a private fallback for the graph-less code path.

### 5.4 DI registration

`OntologyServiceCollectionExtensions.AddOntology` already registers `OntologyGraph` as a singleton. When `InMemoryObjectSetProvider` is registered through DI, the container can inject the graph into the new constructor:

```csharp
// Existing registration pattern — works if InMemoryObjectSetProvider
// is resolved by the container (graph injected automatically)
services.AddSingleton<IObjectSetProvider, InMemoryObjectSetProvider>();
```

Manual construction continues to work:
```csharp
// Without graph (legacy)
var provider = new InMemoryObjectSetProvider();

// With graph (new)
var provider = new InMemoryObjectSetProvider(graph);
```

---

## 6. Testing Strategy

### 6.1 InMemoryExpressionEvaluator tests

New test class: `InMemoryExpressionEvaluatorTests`

| Test | Validates |
|---|---|
| `Evaluate_RootExpression_ReturnsAllItems` | Base case — items resolved by descriptor name |
| `Evaluate_FilterExpression_AppliesPredicate` | Lambda compilation and filtering |
| `Evaluate_FilterChain_AppliesAllPredicates` | Multiple `.Where()` calls compose correctly |
| `Evaluate_IncludeExpression_PassesThrough` | Include doesn't affect data |
| `Evaluate_TraverseLink_ReturnsTargetTypeItems` | Schema-level link traversal |
| `Evaluate_TraverseLink_UnknownLink_Throws` | Error handling for missing link |
| `Evaluate_TraverseLink_WithFilterOnSource_IgnoresSourceFilter` | Schema-level semantics |
| `Evaluate_TraverseLink_ThenFilter_FiltersTargetItems` | Filter after traversal |
| `Evaluate_InterfaceNarrow_FiltersToImplementors` | CLR type assignability filter |
| `Evaluate_InterfaceNarrow_NoImplementors_ReturnsEmpty` | No matching types |
| `Evaluate_RawFilter_ThrowsNotSupported` | Explicit unsupported path |
| `Evaluate_UnknownDescriptor_Throws` | Error handling for missing type |
| `Evaluate_EmptyItemResolver_ReturnsEmpty` | Empty data source |
| `Evaluate_ConcurrentCalls_ThreadSafe` | Stateless after construction |

### 6.2 Link description tests

Extend existing builder tests:

| Test | Validates |
|---|---|
| `HasMany_WithDescription_SetsDescriptorDescription` | Builder → descriptor flow |
| `HasOne_WithDescription_SetsDescriptorDescription` | Single-link variant |
| `HasMany_WithoutDescription_DescriptionIsNull` | Backward compatibility |
| `CrossDomainLink_WithDescription_SetsDescription` | Cross-domain builder flow |
| `CrossDomainLink_Description_ThreadedToResolved` | Description survives resolution |
| `ExploreTool_Links_IncludesDescription` | MCP tool output |
| `ExploreTool_Traversal_IncludesDescription` | MCP traversal output |

### 6.3 InMemoryObjectSetProvider refactor tests

Existing tests must pass unchanged (no graph constructor). New tests for graph-aware path:

| Test | Validates |
|---|---|
| `ExecuteAsync_WithGraph_TraverseLink_Works` | End-to-end traversal |
| `ExecuteAsync_WithGraph_InterfaceNarrow_Works` | End-to-end narrowing |
| `ExecuteAsync_WithoutGraph_LegacyBehavior` | Backward compatibility |
| `ExecuteSimilarityAsync_WithGraph_SourceFiltered` | Evaluator used for source filtering |

---

## 7. Consumer Integration Guide

### 7.1 How Valkyrie uses the evaluator

Valkyrie's `GameLoopObjectSetProvider` in the Agent assembly:

```csharp
public sealed class GameLoopObjectSetProvider : IObjectSetProvider
{
    private readonly InMemoryExpressionEvaluator _evaluator;
    private readonly IFrameStateProvider _frameState;
    private readonly Dictionary<string, Func<FrameState, IReadOnlyList<object>>> _resolvers;

    public GameLoopObjectSetProvider(
        OntologyGraph graph,
        IFrameStateProvider frameState)
    {
        _evaluator = new InMemoryExpressionEvaluator(graph);
        _frameState = frameState;

        // Register resolver per descriptor name
        _resolvers = new()
        {
            ["WedgeFormation"] = _ => [WedgeFormation.Instance],
            ["BoxFormation"] = _ => [BoxFormation.Instance],
            // ... other static configurations ...
            ["TacticalEntity"] = frame => frame.GetEntitySnapshots(),
            ["TacticalTeam"] = frame => frame.GetTeamSnapshots(),
        };
    }

    public Task<ObjectSetResult<T>> ExecuteAsync<T>(
        ObjectSetExpression expression, CancellationToken ct) where T : class
    {
        var items = _evaluator.Evaluate<T>(expression, ResolveItems);
        return Task.FromResult(
            new ObjectSetResult<T>(items, items.Count, ObjectSetInclusion.Properties));
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(
        ObjectSetExpression expression,
        [EnumeratorCancellation] CancellationToken ct) where T : class
    {
        var items = _evaluator.Evaluate<T>(expression, ResolveItems);
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }

    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct) where T : class
    {
        // Valkyrie doesn't need vector similarity for game state — return empty
        return Task.FromResult(new ScoredObjectSetResult<T>([], 0, ObjectSetInclusion.Properties, []));
    }

    private IReadOnlyList<object> ResolveItems(string descriptorName)
    {
        // Capture frame snapshot once per query (consistent-read)
        var frame = _frameState.Current;
        return _resolvers.TryGetValue(descriptorName, out var resolver)
            ? resolver(frame)
            : [];
    }
}
```

**Key properties:**
- Evaluator is created once at construction time (graph reference is frozen)
- Each `ExecuteAsync` call captures a single frame snapshot via `IFrameStateProvider.Current` (volatile read) — all item resolution within that query sees the same frame
- Static configuration objects (formations, threat types) return singletons
- Runtime entities (units, teams) project from frame state

### 7.2 How test harnesses use the evaluator

```csharp
// Test that ontology queries work against fixture data
var graph = BuildTestGraph();
var evaluator = new InMemoryExpressionEvaluator(graph);

var items = evaluator.Evaluate<WedgeFormation>(
    expression,
    descriptorName => descriptorName switch
    {
        "WedgeFormation" => [testWedge],
        "BoxFormation" => [testBox],
        _ => []
    });
```

---

## 8. File Change Summary

| File | Change |
|---|---|
| `src/Strategos.Ontology/ObjectSets/InMemoryExpressionEvaluator.cs` | **NEW** — public evaluator class |
| `src/Strategos.Ontology/ObjectSets/InMemoryObjectSetProvider.cs` | Refactor — new constructors, delegate to evaluator |
| `src/Strategos.Ontology/Descriptors/LinkDescriptor.cs` | Add `Description` property |
| `src/Strategos.Ontology/Descriptors/CrossDomainLinkDescriptor.cs` | Add `Description` property |
| `src/Strategos.Ontology/ResolvedCrossDomainLink.cs` | Add `Description` parameter |
| `src/Strategos.Ontology/Builder/ILinkBuilder.cs` | Add `Description()` method |
| `src/Strategos.Ontology/Builder/LinkBuilder.cs` | Implement `Description()` |
| `src/Strategos.Ontology/Builder/ICrossDomainLinkBuilder.cs` | Add `Description()` method |
| `src/Strategos.Ontology/Builder/CrossDomainLinkBuilder.cs` | Implement `Description()` |
| `src/Strategos.Ontology/OntologyGraphBuilder.cs` | Thread description to `ResolvedCrossDomainLink` |
| `src/Strategos.Ontology.MCP/OntologyExploreTool.cs` | Include descriptions in link/traversal output |
| `tests/Strategos.Ontology.Tests/ObjectSets/InMemoryExpressionEvaluatorTests.cs` | **NEW** — evaluator test class |
| `tests/Strategos.Ontology.Tests/Builder/*` | Extend link description tests |
| `tests/Strategos.Ontology.MCP.Tests/*` | Extend explore tool tests |

---

## 9. Future Extensions

These are **not** in scope but are explicitly enabled by this design:

1. **Instance-level link traversal** — The evaluator's `TraverseLinkExpression` handler could be extended to accept an `ILinkInstanceResolver` that maps source objects to target object IDs. The schema-level fallback remains the default.

2. **`RawFilterExpression` evaluation** — A string expression parser (e.g., simple property comparisons: `"Spacing > 2.5"`) could be added to the evaluator. The `NotSupportedException` is a clear extension point.

3. **`Strategos.Ontology.InMemory` package extraction** — If the evaluator grows significantly (instance-level traversal, raw filter parsing, etc.), it can be extracted to its own package. The public API doesn't change — just the assembly it lives in.

4. **`QueryContext` on provider interfaces** — If multiple consumers need snapshot metadata to flow through the provider contract, `ExecuteAsync` could accept an optional `QueryContext`. For now, consumers handle this internally (Valkyrie captures frame state in its item resolver closure).
