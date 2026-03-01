# Agentic Ontology: Semantic Type System for Agentic Operations

> **Note:** The authoritative specification for the ontology layer is
> `docs/reference/platform-architecture.md` §4.14. This design document captures the
> initial runtime-first proposal (Revision 2). The platform architecture doc incorporates
> additional schema refinements (preconditions, lifecycles, derivation chains, interface
> actions, extension points) and specifies a compile-time source generation architecture
> as the target. The current implementation follows a hybrid approach: runtime-first
> architecture with the full schema refinement vocabulary. Migration to compile-time
> source generation is planned as a separate effort.

> **Scope:** Library design for `Strategos.Ontology` NuGet packages (lives in strategos repo).
> Basileus is the first consumer; this repo receives ADR updates only.
>
> **Revision 2** — Incorporates brainstorming decisions: runtime-first architecture,
> Object Set algebra, first-class events, Marten-backed instance queries,
> ontology-enriched telemetry. See [Palantir Ontology reference](https://www.palantir.com/docs/foundry/ontology/overview).

---

## 1. Problem Statement

Three deficiencies in the current platform:

1. **Domain silos.** Trading, StyleEngine, and Knowledge are architecturally independent assemblies with no shared type vocabulary. An agent that ingests a document (Knowledge) and wants to inform a trading strategy has no typed mechanism to express that relationship.

2. **Flat tool discovery.** MCP tools are flat lists with string descriptions. Progressive disclosure tells agents *what tools exist* but not *what types flow between them*. An agent cannot ask "what actions can I take on a Position?" — it must grep tool descriptions.

3. **Blind planning.** Agents plan using natural language reasoning over unstructured context. There is no runtime world model that constrains which operations are valid, which types they accept/produce, or how workflows chain together.

4. **Untyped telemetry.** Events carry system metadata (tool name, duration, tokens) but not domain context. Observability queries require human domain knowledge to interpret — there is no way to ask "which actions on which object types cost the most tokens."

Palantir's Foundry Ontology solves the first three with a semantic layer: Object Types + Link Types + Action Types + Interfaces, backed by a microservices architecture (OMS + OSS + Object Storage V2 + OSDK). We adapt their architectural insights for the Strategos ecosystem, mapping each Palantir component onto our existing infrastructure.

---

## 2. Design Principles

| Principle | Implication |
|-----------|------------|
| Agent-first, developer-second | Runtime query performance over compile-time DX; Object Set algebra as primary interface |
| Runtime-first (EF Core model) | Fluent builder populates `OntologyGraph` at startup; source generators emit diagnostics only, not runtime code |
| Extend progressive disclosure | Ontology metadata enriches tool stubs, follows 3-level skill progressive disclosure pattern |
| First-class events | `ObjectTypeDescriptor` includes `Events` collection from day one; event-driven link materialization via Marten projections |
| Deterministic structure for stochastic agents | Typed action graph constrains agent planning (CMDP action space reduction) |
| Domain independence preserved | Cross-domain links resolved at composition time, not declaration time |
| NuGet package | Reusable across any Strategos consumer, not Basileus-specific |
| Palantir-aligned architecture | OMS → OntologyGraph, OSS → ObjectSet algebra, Object Storage V2 → Marten, OSDK → MCP tools |

**Priority ordering for design trade-offs:** Agents (runtime) > Exarchos (orchestrator) > Developers (compile-time).

---

## 3. Palantir → Agentic Ontology Architecture Mapping

### 3.1 Component Mapping

| Palantir Component | Role | Agentic Equivalent | Implementation |
|---|---|---|---|
| **OMS** (Ontology Metadata Service) | Runtime type registry | `OntologyGraph` | Built by fluent DSL at startup, in-memory |
| **Object Set Service** (OSS) | Composable query algebra | `ObjectSet<T>` algebra | Expression tree translated to Marten LINQ |
| **Object Storage V2** | Scalable object store | Marten document store | PostgreSQL-backed, event-sourced |
| **Object Data Funnel** | Ingests data sources into store | Marten event projections | Domain events materialize links and state |
| **Actions Service** | Structured mutations + audit | Action dispatch layer | Routes to Wolverine sagas, emits Marten events |
| **OSDK** (code-generated SDK) | Type-safe client bindings | MCP tool surface | `ontology_query`, `ontology_action`, `ontology_explore` |
| **Security policies** | Per-object permissions | ControlPlane Policy Engine | Pre/post-execution validation (existing) |

### 3.2 Three-Part Structure (Palantir's Language / Engine / Toolchain)

```text
┌─────────────────────────────────────────────────┐
│  TOOLCHAIN — Agent-Facing Surface               │
│  MCP Tools: ontology_query, ontology_action      │
│  Progressive Disclosure: enriched .pyi stubs     │
│  Exarchos: graph traversal for planning          │
├─────────────────────────────────────────────────┤
│  ENGINE — Runtime Query & Mutation               │
│  ObjectSet<T> algebra (schema + instance queries) │
│  Action dispatch (workflow/tool binding)          │
│  Event stream (temporal queries)                 │
├─────────────────────────────────────────────────┤
│  LANGUAGE — Domain Definition                    │
│  DomainOntology.Define() fluent DSL              │
│  OntologyGraph (runtime type registry)           │
│  Descriptors: Object, Property, Link, Action,    │
│               Event, Interface, Domain           │
├─────────────────────────────────────────────────┤
│  PERSISTENCE — Marten + PostgreSQL               │
│  Event store (domain events)                     │
│  Projections (link materialization, state views)  │
│  Document store (object instances)               │
└─────────────────────────────────────────────────┘
```

### 3.3 Key Architectural Difference from Palantir

Palantir's Ontology **owns storage** (Object Storage V2 is a dedicated persistence layer). Ours **does not** — `Strategos.Ontology` defines abstractions (`IObjectSetProvider`, `IEventStreamProvider`), and consumers provide implementations backed by their persistence layer (Marten in Basileus's case). This keeps domains autonomous while still enabling unified queries through the Object Set algebra.

---

## 4. Package Architecture

```text
strategos/
├── src/
│   ├── Strategos.Ontology/                    # Contracts, DSL, runtime graph, Object Set algebra
│   │   ├── DomainOntology.cs                # Base class for domain modules
│   │   ├── OntologyGraph.cs                 # Runtime type registry (in-memory)
│   │   ├── OntologyGraphBuilder.cs          # Internal builder that DomainOntology.Define() drives
│   │   ├── Builder/                         # Fluent API interfaces
│   │   │   ├── IOntologyBuilder.cs          # Entry point
│   │   │   ├── IObjectTypeBuilder.cs        # Object type configuration
│   │   │   ├── IActionBuilder.cs            # Action definition
│   │   │   ├── IEventBuilder.cs             # Event declaration
│   │   │   ├── IInterfaceBuilder.cs         # Polymorphic interfaces
│   │   │   └── ICrossDomainLinkBuilder.cs   # Cross-domain relationships
│   │   ├── Descriptors/                     # Runtime metadata types
│   │   │   ├── ObjectTypeDescriptor.cs
│   │   │   ├── PropertyDescriptor.cs
│   │   │   ├── LinkDescriptor.cs
│   │   │   ├── ActionDescriptor.cs
│   │   │   ├── EventDescriptor.cs           # NEW: first-class event metadata
│   │   │   ├── InterfaceDescriptor.cs
│   │   │   └── DomainDescriptor.cs
│   │   ├── ObjectSets/                      # Object Set algebra
│   │   │   ├── ObjectSet.cs                 # Composable query expression tree
│   │   │   ├── ObjectSetExpression.cs       # Expression node types
│   │   │   ├── IObjectSetProvider.cs        # Provider abstraction (Marten adapter)
│   │   │   └── ObjectSetResult.cs           # Query result envelope
│   │   ├── Actions/                         # Action dispatch
│   │   │   ├── IActionDispatcher.cs         # Routes actions to workflows/tools
│   │   │   ├── ActionContext.cs             # Telemetry-enriched execution context
│   │   │   └── ActionResult.cs
│   │   ├── Events/                          # Event integration
│   │   │   ├── IEventStreamProvider.cs      # Temporal event queries
│   │   │   ├── IOntologyProjection.cs       # Link materialization contract
│   │   │   └── EventQuery.cs               # Event filter expressions
│   │   ├── Query/                           # Schema query contracts
│   │   │   ├── IOntologyQuery.cs            # Schema-level queries
│   │   │   └── OntologyQueryResult.cs
│   │   ├── Telemetry/                       # Observability enrichment
│   │   │   ├── OntologyTelemetryContext.cs  # Semantic context for events
│   │   │   └── IOntologyMetrics.cs          # Per-action/type metrics
│   │   └── Extensions/                      # Optional integration points
│   │       └── WorkflowOntologyExtensions.cs # Consumes<T>/Produces<T>
│   │
│   ├── Strategos.Ontology.Generators/         # Roslyn analyzer — diagnostics only
│   │   ├── OntologyDiagnosticAnalyzer.cs    # Entry point (DiagnosticAnalyzer)
│   │   └── Analyzers/
│   │       ├── DomainOntologyAnalyzer.cs    # Validates DomainOntology.Define() calls
│   │       ├── PropertyAnalyzer.cs          # Validates expression trees resolve
│   │       ├── CrossDomainLinkAnalyzer.cs   # Validates external references
│   │       └── EventAnalyzer.cs             # Validates event type declarations
│   │
│   └── Strategos.Ontology.MCP/               # MCP tool surface + progressive disclosure
│       ├── OntologyMcpTools.cs              # ontology_query, ontology_action, ontology_explore
│       ├── OntologyStubGenerator.cs         # Enhanced .pyi stub generation
│       └── OntologyToolDiscovery.cs         # Semantic tool discovery
```

**Dependency graph:**

```text
Strategos.Ontology.MCP
    ↓
Strategos.Ontology  ←──  Strategos.Ontology.Generators (analyzer ref, diagnostics only)
    ↓ (optional)
Strategos (for Consumes<T>/Produces<T> extension methods)
```

**Consumer-side packages (Basileus-owned, not in this repo):**

```text
Basileus.Ontology.Marten/          # IObjectSetProvider backed by Marten LINQ
    ↓                               # IEventStreamProvider backed by Marten event store
Strategos.Ontology                   # IOntologyProjection backed by Marten projections
    +
Marten
```

`Strategos.Ontology` has **zero dependency on Marten or any persistence library**. The provider abstractions (`IObjectSetProvider`, `IEventStreamProvider`, `IOntologyProjection`) are contracts — implementations live in consumer assemblies.

---

## 5. Runtime Model

### 5.1 EF Core Pattern — Builder Executes at Startup

The ontology follows the same pattern as EF Core's `DbContext.OnModelCreating()`: the fluent builder executes at application startup and populates an in-memory `OntologyGraph`. No source generator emits runtime code.

```csharp
// At ControlPlane startup (Basileus.AppHost or ControlPlane host)
services.AddOntology(ontology =>
{
    // Register domain ontologies — each calls Define() at startup
    ontology.AddDomain<TradingOntology>();
    ontology.AddDomain<KnowledgeOntology>();
    ontology.AddDomain<StyleEngineOntology>();

    // Register provider implementations (Basileus-specific)
    ontology.UseObjectSetProvider<MartenObjectSetProvider>();
    ontology.UseEventStreamProvider<MartenEventStreamProvider>();
    ontology.UseActionDispatcher<WolverineActionDispatcher>();
});
```

**Startup sequence:**

1. Each `DomainOntology.Define(IOntologyBuilder)` executes, populating descriptor collections
2. `OntologyGraphBuilder` validates cross-domain links (fail-fast on unresolvable references)
3. `OntologyGraphBuilder` validates interface implementations (property type compatibility)
4. `OntologyGraphBuilder` validates workflow chaining (`Produces<T>` → `Consumes<T>`)
5. `OntologyGraph` is frozen (immutable after startup) and registered as a singleton
6. MCP tools are registered against the frozen graph

**Fail-fast validation** replaces compile-time diagnostics for structural errors. The source generator (`Strategos.Ontology.Generators`) provides IDE-time warnings as a supplementary DX enhancement, but the runtime builder is the source of truth.

### 5.2 OntologyGraph — The Runtime Registry

```csharp
/// <summary>
/// Immutable, thread-safe runtime registry of all ontology metadata.
/// Analogous to Palantir's Ontology Metadata Service (OMS).
/// Built once at startup by OntologyGraphBuilder, then frozen.
/// </summary>
public sealed class OntologyGraph
{
    public IReadOnlyList<DomainDescriptor> Domains { get; }
    public IReadOnlyList<ObjectTypeDescriptor> ObjectTypes { get; }
    public IReadOnlyList<InterfaceDescriptor> Interfaces { get; }
    public IReadOnlyList<ResolvedCrossDomainLink> CrossDomainLinks { get; }
    public IReadOnlyList<WorkflowChain> WorkflowChains { get; }

    // Fast lookups (pre-computed at freeze time)
    public ObjectTypeDescriptor? GetObjectType(string domain, string name);
    public IReadOnlyList<ObjectTypeDescriptor> GetImplementors(string interfaceName);
    public IReadOnlyList<LinkTraversalResult> TraverseLinks(
        string domain, string objectTypeName, int maxDepth = 2);
    public IReadOnlyList<WorkflowChain> FindWorkflowChains(string targetWorkflow);
}
```

---

## 6. DSL Specification

### 6.1 DomainOntology — Entry Point

Each domain assembly defines one or more `DomainOntology` subclasses. The `Define` method executes at startup and populates the runtime graph.

```csharp
namespace Basileus.Trading;

public sealed class TradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        // Object types, links, actions, events, interfaces defined here
    }
}
```

### 6.2 Object Types

Object types map existing C# records/classes into the ontology. The `Object<T>()` call declares that `T` participates in the semantic layer. Properties are selectively exposed via expression trees.

```csharp
protected override void Define(IOntologyBuilder builder)
{
    builder.Object<Position>(obj =>
    {
        obj.Key(p => p.Id);
        obj.Property(p => p.Symbol).Required();
        obj.Property(p => p.Quantity);
        obj.Property(p => p.UnrealizedPnL).Computed();
        obj.Property(p => p.OpenedAt);
    });

    builder.Object<TradeOrder>(obj =>
    {
        obj.Key(o => o.OrderId);
        obj.Property(o => o.Side);
        obj.Property(o => o.Price);
        obj.Property(o => o.FilledQuantity);
        obj.Property(o => o.Status);
    });

    builder.Object<Strategy>(obj =>
    {
        obj.Key(s => s.Id);
        obj.Property(s => s.Name).Required();
        obj.Property(s => s.Description);
    });
}
```

**Design decisions:**

- `Object<T>()` references an existing type. The ontology does not generate domain types — it maps them.
- `Key()` designates the identity property. Required for entity resolution and linking.
- `Property()` uses expression trees for member resolution. Only explicitly exposed properties are ontology-visible.
- `.Required()` and `.Computed()` are metadata hints for progressive disclosure stubs and validation.

### 6.3 Links

Links define typed, directional relationships between object types. Three cardinalities are supported, mirroring Palantir's link types.

```csharp
builder.Object<Position>(obj =>
{
    obj.HasMany<TradeOrder>("Orders");
    obj.HasOne<Strategy>("Strategy");
});

builder.Object<Strategy>(obj =>
{
    obj.HasMany<Position>("Positions");
});
```

**Many-to-many with edge types** (for relationships that carry data):

```csharp
builder.Object<AtomicNote>(obj =>
{
    obj.ManyToMany<AtomicNote>("SemanticLinks", edge =>
    {
        edge.Property<LinkType>("Type");
        edge.Property<double>("Confidence");
        edge.Property<string>("ContextDescription");
    });
});
```

For complex edges backed by existing C# types:

```csharp
obj.ManyToMany<AtomicNote>("SemanticLinks")
    .WithEdge<KnowledgeLink>(edge =>
    {
        edge.MapProperty(l => l.Type);
        edge.MapProperty(l => l.Confidence);
    });
```

### 6.4 Actions

Actions are operations that can be performed on an object type. Each action declares its input/output types and its execution binding.

```csharp
builder.Object<Position>(obj =>
{
    // Workflow-backed action (durable, multi-step, saga state)
    obj.Action("ExecuteTrade")
        .Description("Open a new position via the trade execution workflow")
        .Accepts<TradeExecutionRequest>()
        .Returns<TradeExecutionResult>()
        .BoundToWorkflow("execute-trade");

    // Tool-backed action (immediate, single-step)
    obj.Action("GetQuote")
        .Description("Fetch current market quote for this position's symbol")
        .Accepts<QuoteRequest>()
        .Returns<Quote>()
        .BoundToTool("MarketDataMcpTools", "GetQuoteAsync");

    // Unbound action (implementation registered separately by consumer)
    obj.Action("Hedge")
        .Description("Hedge this position against adverse movement")
        .Accepts<HedgeRequest>()
        .Returns<HedgeResult>();
});
```

### 6.5 Events — First-Class Event Awareness

Events are first-class members of object types. Each event declaration specifies the CLR event type and its effects on the object's state and links.

```csharp
builder.Object<Position>(obj =>
{
    // Declare events this object type can emit
    obj.Event<TradeExecuted>(evt =>
    {
        evt.Description("A trade was executed against this position");

        // Event-driven link materialization (Marten projection registered automatically)
        evt.MaterializesLink("Orders", e => e.OrderId);

        // Event-driven property updates
        evt.UpdatesProperty(p => p.UnrealizedPnL, e => e.NewPnL);
    });

    obj.Event<PositionUpdated>(evt =>
    {
        evt.Description("Position state was recalculated");
        evt.UpdatesProperty(p => p.Quantity, e => e.NewQuantity);
    });

    obj.Event<RiskThresholdBreached>(evt =>
    {
        evt.Description("Risk threshold exceeded for this position");
        evt.Severity(EventSeverity.Alert);
    });
});
```

**Design decisions:**

- Events are declared per object type, producing an `Events` collection on `ObjectTypeDescriptor`.
- `MaterializesLink()` registers a Marten projection handler via `IOntologyProjection`. When the event fires, the projection materializes the specified link.
- `UpdatesProperty()` declares which properties change when the event fires — enables agents to reason about event effects without executing them.
- `Severity()` classifies events for observability (Info, Warning, Alert, Critical).
- Adding events later is **non-breaking** — the `Events` collection is additive.

### 6.6 Interfaces — Polymorphic Object Types

Interfaces define shared shapes across object types, enabling cross-domain polymorphic queries.

```csharp
builder.Interface<ISearchable>("Searchable", iface =>
{
    iface.Property(s => s.Title);
    iface.Property(s => s.Description);
    iface.Property(s => s.Embedding);
});

builder.Object<Position>(obj =>
{
    obj.Implements<ISearchable>(map =>
    {
        map.Via(p => p.Symbol, s => s.Title);
        map.Via(p => p.DisplayDescription, s => s.Description);
        map.Via(p => p.SearchEmbedding, s => s.Embedding);
    });
});
```

**Agent query enabled:** "Find all `Searchable` objects matching 'machine learning'" returns results from any domain transparently.

### 6.7 Cross-Domain Links

Domains are independently compiled. Cross-domain relationships use string-based external references, resolved at startup.

```csharp
builder.CrossDomainLink("KnowledgeInformsStrategy")
    .From<AtomicNote>()
    .ToExternal("trading", "Strategy")
    .ManyToMany()
    .WithEdge(edge =>
    {
        edge.Property<double>("Relevance");
        edge.Property<string>("Rationale");
    });
```

**Resolution:** `OntologyGraphBuilder` validates at startup that `"trading"."Strategy"` exists. Unresolvable references throw `OntologyCompositionException` (fail-fast).

### 6.8 Workflow Integration (Optional Extension)

When both `Strategos.Ontology` and `Strategos` are referenced:

```csharp
var workflow = Workflow<TradeExecutionState>
    .Create("execute-trade")
    .Consumes<Position>()
    .Produces<TradeOrder>()
    .StartWith<ValidateOrder>()
    .Then<RouteToExchange>()
    .Then<ConfirmExecution>()
    .Finally<UpdatePosition>();
```

`Consumes<T>()` and `Produces<T>()` are extension methods that register the workflow in the `OntologyGraph` at startup:

1. Register the workflow as an action on the consumed type (auto-binding)
2. Enable workflow chaining inference (`Produces<T>` → `Consumes<T>` compatibility)
3. Give agents a typed dependency graph for multi-step planning

---

## 7. Object Set Algebra

### 7.1 Core Concept

An `ObjectSet<T>` is a lazy, composable query expression — analogous to `IQueryable<T>` but operating over ontology-typed domain objects. It is the **primary primitive** that agents interact with, mirroring Palantir's Object Set Service.

```csharp
/// <summary>
/// Composable query expression over ontology-typed objects.
/// Builds an expression tree that is translated by IObjectSetProvider
/// into the appropriate persistence query (Marten LINQ, SQL, etc.).
/// </summary>
public sealed class ObjectSet<T> where T : class
{
    // Filtering
    public ObjectSet<T> Where(Expression<Func<T, bool>> predicate);

    // Link traversal (returns a new ObjectSet of the linked type)
    public ObjectSet<TLinked> TraverseLink<TLinked>(string linkName) where TLinked : class;

    // Interface narrowing (cross-domain polymorphic query)
    public ObjectSet<TInterface> OfInterface<TInterface>() where TInterface : class;

    // Action application (batch-capable)
    public Task<IReadOnlyList<ActionResult>> ApplyAsync(
        string actionName, object request, CancellationToken ct = default);

    // Event queries (temporal)
    public IAsyncEnumerable<OntologyEvent> EventsAsync(
        TimeSpan? since = null, IReadOnlyList<string>? eventTypes = null);

    // Projection selection (control what's included in results)
    public ObjectSet<T> Include(ObjectSetInclusion inclusion);

    // Materialization
    public Task<ObjectSetResult<T>> ExecuteAsync(CancellationToken ct = default);
    public IAsyncEnumerable<T> StreamAsync(CancellationToken ct = default);
}
```

### 7.2 ObjectSet Inclusion (Progressive Data Loading)

Agents control what data is returned per query, following progressive disclosure:

```csharp
[Flags]
public enum ObjectSetInclusion
{
    Properties = 1,      // Object properties
    Actions = 2,         // Available actions with signatures
    Links = 4,           // Direct link descriptors
    Events = 8,          // Recent events (configurable window)
    Interfaces = 16,     // Implemented interfaces
    LinkedObjects = 32,  // Resolved linked object instances (1-hop)
    Schema = Properties | Actions | Links | Interfaces,  // Schema-only (no data)
    Full = Schema | Events | LinkedObjects               // Everything
}
```

### 7.3 Provider Abstraction

```csharp
/// <summary>
/// Translates ObjectSet expression trees into persistence queries.
/// Consumers implement this for their specific persistence layer.
/// Basileus provides MartenObjectSetProvider.
/// </summary>
public interface IObjectSetProvider
{
    Task<ObjectSetResult<T>> ExecuteAsync<T>(
        ObjectSetExpression expression, CancellationToken ct = default) where T : class;

    IAsyncEnumerable<T> StreamAsync<T>(
        ObjectSetExpression expression, CancellationToken ct = default) where T : class;
}
```

### 7.4 Agent Interaction Example

```csharp
// Inside Phronesis ThinkStep — assembling context for a Position task
var context = await ontology.ObjectSet<Position>()
    .Where(p => p.Id == task.TargetObjectId)
    .Include(ObjectSetInclusion.Full)
    .ExecuteAsync(ct);

// Result includes typed properties, available actions, linked objects,
// recent events, and interface information — all in one composable query.
```

Via MCP tool (JSON representation for agent consumption):

```json
{
  "tool": "ontology_query",
  "params": {
    "objectType": "Position",
    "filter": { "UnrealizedPnL": { "gt": 10000 } },
    "include": ["properties", "actions", "links", "events"],
    "events": { "since": "1h" }
  }
}
```

---

## 8. Source Generator — Diagnostics Only

### 8.1 Role Clarification

The source generator (`Strategos.Ontology.Generators`) is a **Roslyn DiagnosticAnalyzer**, not an `IIncrementalGenerator`. It emits **zero runtime code**. Its sole purpose is IDE-time validation — red squiggles and warnings that supplement the runtime builder's fail-fast validation.

### 8.2 Diagnostic Catalog

| Code | Severity | Condition |
|------|----------|-----------|
| `ONTO001` | Error | Object type has no `Key()` declaration |
| `ONTO002` | Error | Property expression references non-existent member |
| `ONTO003` | Warning | Cross-domain link references unknown domain (can't validate at compile-time across assemblies) |
| `ONTO004` | Info | Object type has no actions (pure data, not actionable) |
| `ONTO005` | Error | Interface mapping references incompatible property types |
| `ONTO006` | Warning | Workflow `Produces<T>` has no matching `Consumes<T>` consumer in same assembly |
| `ONTO007` | Error | Duplicate object type registration in same domain |
| `ONTO008` | Warning | Event type not declared on any object type |
| `ONTO009` | Error | Event `MaterializesLink` references undeclared link name |
| `ONTO010` | Warning | Object type has events but no `IEventStreamProvider` likely registered |

**Note:** Cross-assembly validation (ONTO003, ONTO006) can only produce warnings, not errors, because the analyzer can't see other assemblies' `DomainOntology` classes. Full validation happens at runtime via `OntologyGraphBuilder`.

---

## 9. MCP Tool Surface

### 9.1 Design Principles (from Skill-Building Best Practices)

The MCP tool surface follows Anthropic's skill-building best practices:

- **Progressive disclosure:** Level 1 (tool listing) → Level 2 (rich descriptions) → Level 3 (full typed responses)
- **Problem-first framing:** Agents describe outcomes, tools route to the right data
- **Context-aware tool selection:** One composable query endpoint rather than separate tools per layer
- **Domain-specific intelligence:** Ontology context embedded in every response

### 9.2 MCP Tool Catalog

Three tools, following the "minimal surface, maximum composability" principle:

#### `ontology_query` — Read Operations

```json
{
  "name": "ontology_query",
  "description": "Query the ontology for object types, instances, links, events, and actions. Use when exploring what's available in a domain, finding objects matching criteria, or understanding relationships between types.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "objectType": { "type": "string", "description": "Object type name (e.g., 'Position')" },
      "domain": { "type": "string", "description": "Domain name (e.g., 'trading'). Optional if objectType is unique." },
      "filter": { "type": "object", "description": "Property filters (e.g., {\"UnrealizedPnL\": {\"gt\": 10000}})" },
      "traverseLink": { "type": "string", "description": "Link name to traverse (returns linked objects)" },
      "interface": { "type": "string", "description": "Filter to objects implementing this interface" },
      "include": {
        "type": "array",
        "items": { "type": "string", "enum": ["properties", "actions", "links", "events", "interfaces", "linkedObjects"] },
        "description": "What to include in results. Defaults to schema-only."
      },
      "events": {
        "type": "object",
        "properties": {
          "since": { "type": "string", "description": "Time window (e.g., '1h', '24h')" },
          "types": { "type": "array", "items": { "type": "string" } }
        }
      }
    },
    "required": ["objectType"]
  }
}
```

#### `ontology_action` — Write Operations

```json
{
  "name": "ontology_action",
  "description": "Execute an action on one or more objects. Routes to the bound workflow or tool. Use when you need to modify state, trigger workflows, or perform operations.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "objectType": { "type": "string" },
      "objectId": { "type": "string", "description": "Target object ID (or omit for batch via filter)" },
      "filter": { "type": "object", "description": "Alternative: apply action to all matching objects" },
      "action": { "type": "string", "description": "Action name (e.g., 'ExecuteTrade')" },
      "request": { "type": "object", "description": "Action input payload" }
    },
    "required": ["objectType", "action", "request"]
  }
}
```

#### `ontology_explore` — Schema Discovery

```json
{
  "name": "ontology_explore",
  "description": "Explore the ontology schema: list domains, object types, actions, links, interfaces, events, and workflow chains. Use when planning which actions to take or understanding the domain model.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "scope": {
        "type": "string",
        "enum": ["domains", "objectTypes", "actions", "links", "interfaces", "events", "workflowChains"],
        "description": "What aspect of the schema to explore"
      },
      "domain": { "type": "string", "description": "Filter to a specific domain" },
      "objectType": { "type": "string", "description": "Filter to a specific object type" },
      "traverseFrom": { "type": "string", "description": "Start graph traversal from this object type" },
      "maxDepth": { "type": "integer", "default": 2 }
    },
    "required": ["scope"]
  }
}
```

### 9.3 Progressive Disclosure Integration

At sandbox creation, the ControlPlane generates enriched tool stubs from the `OntologyGraph`:

```python
# servers/trading/position.pyi — generated at sandbox creation
class Position:
    """trading.Position — tradable financial position

    Properties:
        symbol: str (required)
        quantity: float
        unrealized_pnl: float (computed)

    Links:
        orders -> TradeOrder[] (one-to-many)
        strategy -> Strategy (many-to-one)

    Actions:
        execute_trade(request: TradeExecutionRequest) -> TradeExecutionResult
            Bound to workflow: execute-trade. Produces: TradeOrder
        get_quote(request: QuoteRequest) -> Quote
            Bound to tool: MarketDataMcpTools.GetQuoteAsync
        hedge(request: HedgeRequest) -> HedgeResult

    Events:
        TradeExecuted -> materializes Orders link, updates UnrealizedPnL
        PositionUpdated -> updates Quantity
        RiskThresholdBreached (severity: alert)

    Interfaces: Searchable
    """
```

---

## 10. Event Integration and Marten Projections

### 10.1 Event-Driven Link Materialization

When a `DomainOntology` declares `evt.MaterializesLink()`, the ontology registers an `IOntologyProjection` handler. In the Marten implementation, this translates to a Marten `IProjection`:

```csharp
// Auto-registered by Basileus.Ontology.Marten based on ontology declarations
public class PositionOrdersLinkProjection : SingleStreamProjection<PositionOrdersLink>
{
    public void Apply(TradeExecuted evt, PositionOrdersLink link)
    {
        link.PositionId = evt.PositionId;
        link.TradeOrderId = evt.OrderId;
        link.MaterializedAt = evt.Timestamp;
    }
}
```

Links are always consistent — derived from the immutable event stream, not stale caches.

### 10.2 Temporal Event Queries

The `IEventStreamProvider` enables temporal queries over domain events:

```csharp
public interface IEventStreamProvider
{
    IAsyncEnumerable<OntologyEvent> QueryEventsAsync(
        string domain,
        string objectTypeName,
        string? objectId = null,
        TimeSpan? since = null,
        IReadOnlyList<string>? eventTypes = null,
        CancellationToken ct = default);
}
```

In the Marten implementation, this reads directly from the Marten event store with appropriate filters.

### 10.3 Event Causality Chains

First-class events enable typed causality tracing across the system:

```text
Agent request: "Hedge all positions where PnL < -5000"

Causality chain (auto-captured):
1. ontology_query: Position.Where(PnL < -5000) → 3 objects
2. ontology_action: Position[pos-123].Hedge(HedgeRequest)
   → Event: HedgeRequested
   → Event: TradeExecuted { orderId: "ord-456" }
   → Materialization: Position→TradeOrder link
   → Event: PositionUpdated { PnL: -2300 → -1100 }
3. ontology_action: Position[pos-456].Hedge(HedgeRequest)
   ...
```

---

## 11. Telemetry and Observability

### 11.1 Ontology-Enriched Telemetry Context

Every action dispatched through the ontology automatically enriches telemetry events with semantic context:

```csharp
/// <summary>
/// Attached to every Marten event emitted through ontology action dispatch.
/// Transforms flat system telemetry into typed, domain-queryable signals.
/// </summary>
public sealed record OntologyTelemetryContext
{
    public required string Domain { get; init; }
    public required string ObjectType { get; init; }
    public required string? ObjectId { get; init; }
    public required string Action { get; init; }
    public required string? InputType { get; init; }
    public required string? OutputType { get; init; }
    public required IReadOnlyList<string> TraversedLinks { get; init; }
    public required IReadOnlyList<string> ProducedEvents { get; init; }
    public required IReadOnlyList<string> MaterializedLinks { get; init; }
}
```

### 11.2 Amplification Across Feedback Loops

**Loop 1–2 (Thompson Sampling):** Strategy outcomes gain dimensional context — per-(objectType, action, linkContext) priors instead of flat per-strategy priors. Thompson Sampling selects strategies based on the typed context of the current task.

**Loop 3 (Task Router):** Ontology graph complexity (link count, cross-domain link presence) becomes a routing factor. Tasks with high graph complexity route to higher-capability tiers.

**Loop 4 (Profile Evolution):** Execution profiles auto-tune per-object-type. `Trading.FundamentalAnalysis.RagConfig.TopK` can be 12 for Position (complex) and 5 for TradeOrder (simple).

**Loop 5 (Knowledge Enrichment):** Link materializations are telemetry events. Exarchos knows when new knowledge-strategy connections form.

**Loop 6 (Panoptikon):** Production incidents correlate to ontology actions and object types, not just tools and services. "TradeExecution failures" → "Position.ExecuteTrade action" → root cause in ontology graph.

### 11.3 OntologyMetricsView (New CQRS View)

A Marten projection materializes ontology-specific metrics alongside the existing 7 CQRS views:

```csharp
public sealed class OntologyMetricsView
{
    /// <summary>Per-action latency, success rate, token consumption.</summary>
    public IReadOnlyDictionary<string, ActionMetrics> ActionMetrics { get; init; }

    /// <summary>Per-object-type event frequency and distribution.</summary>
    public IReadOnlyDictionary<string, EventFrequency> EventFrequency { get; init; }

    /// <summary>Link materialization rates and lag.</summary>
    public IReadOnlyDictionary<string, LinkMaterializationRate> LinkHealth { get; init; }

    /// <summary>Which links are actually traversed by agents vs. declared but unused.</summary>
    public IReadOnlyList<TraversalPattern> TraversalHotPaths { get; init; }

    /// <summary>Thompson Sampling priors conditioned on ontology context.</summary>
    public IReadOnlyDictionary<string, ConditionalPrior> ConditionalPriors { get; init; }
}
```

---

## 12. Platform Integration

### 12.1 Phronesis ThinkStep — Ontology as Context Source

The ThinkStep currently references `ComposedOntology` for context assembly. With the ontology layer, this becomes a concrete `ObjectSet` query:

```csharp
// In ThinkStep — assembling context for the current task
var taskContext = await ontology.ObjectSet<Position>()
    .Where(p => p.Id == task.TargetObjectId)
    .Include(ObjectSetInclusion.Full)
    .ExecuteAsync(ct);

// Uses result to:
// 1. Constrain execution profile tool subset (only actions on Position)
// 2. Enrich LLM prompt with typed context
// 3. Identify workflow chains for multi-step planning
```

### 12.2 Execution Profiles — Ontology-Driven Tool Subsetting

```csharp
// Derive tool subset from object type's declared actions
var profile = ExecutionProfile.FromOntology(ontology, "trading", "Position");
// Automatically includes: ExecuteTrade, GetQuote, Hedge
// Plus tools from linked types reachable within configurable hop depth
```

This directly reduces the CMDP action space — instead of evaluating all ~100+ tools, the ontology constrains to the 3–8 actions relevant to the target object type.

### 12.3 Exarchos — Strategic Planning via Ontology Graph

Exarchos queries the ontology through the Workflow MCP Server for multi-step planning:

```text
Exarchos query: "How do I get from ingested knowledge to an informed trade?"

Ontology graph traversal response:
  AtomicNote --[KnowledgeInformsStrategy]--> Strategy
  Strategy --[HasMany]--> Position
  Position --[Action:ExecuteTrade]--> TradeOrder

  Workflow chain:
    1. ingest-knowledge (produces: AtomicNote)
    2. [cross-domain link: KnowledgeInformsStrategy]
    3. execute-trade (consumes: Position, produces: TradeOrder)
```

### 12.4 ControlPlane Hosting

The `OntologyGraph` is hosted **in-process** on the ControlPlane:

```csharp
// In ControlPlane startup
builder.Services.AddOntology(ontology =>
{
    ontology.AddDomain<TradingOntology>();
    ontology.AddDomain<KnowledgeOntology>();
    ontology.AddDomain<StyleEngineOntology>();
    ontology.UseObjectSetProvider<MartenObjectSetProvider>();
    ontology.UseEventStreamProvider<MartenEventStreamProvider>();
    ontology.UseActionDispatcher<WolverineActionDispatcher>();
});

// MCP tools registered automatically from ontology
builder.Services.AddOntologyMcpTools();
```

---

## 13. Basileus Adoption Strategy

### What Changes in This Repo (strategos)

1. **`Strategos.Ontology` package:** Contracts, DSL, runtime graph, Object Set algebra, telemetry enrichment
2. **`Strategos.Ontology.Generators` package:** DiagnosticAnalyzer for IDE-time validation (diagnostics only)
3. **`Strategos.Ontology.MCP` package:** MCP tool definitions (`ontology_query`, `ontology_action`, `ontology_explore`), stub generator

### What Changes in Basileus Repo

1. **ADR update:** Update `platform-architecture.md` ontology sections to reflect runtime-first architecture, Object Set algebra, first-class events, telemetry integration
2. **`Basileus.Ontology.Marten` package:** `IObjectSetProvider`, `IEventStreamProvider`, `IOntologyProjection` implementations backed by Marten
3. **Domain ontology classes:** Each domain assembly adds a `DomainOntology` subclass
4. **ControlPlane registration:** `AddOntology()` with all domain ontologies and Marten providers
5. **Phronesis ThinkStep update:** Use `ObjectSet` queries for context assembly
6. **Execution profile enhancement:** `FromOntology()` factory for ontology-driven tool subsetting
7. **`ISearchable` interface:** Shared interface implemented by key domain types
8. **`OntologyMetricsView`:** New Marten projection for ontology-specific observability

### What Does NOT Change

- Domain types (Position, AtomicNote, StyleCard) — unchanged, ontology maps them
- Existing MCP tools — unchanged, ontology binds to them
- Existing workflows — unchanged, optional `Consumes`/`Produces` added incrementally
- Existing event types — unchanged, ontology declares awareness of them
- Domain independence — preserved, cross-domain links resolved at composition

---

## 14. Illustrative Example: Full Domain Ontology

Complete `KnowledgeOntology` showing all DSL features:

```csharp
namespace Basileus.Knowledge;

public sealed class KnowledgeOntology : DomainOntology
{
    public override string DomainName => "knowledge";

    protected override void Define(IOntologyBuilder builder)
    {
        // -- Interfaces --

        builder.Interface<ISearchable>("Searchable", iface =>
        {
            iface.Property(s => s.Title);
            iface.Property(s => s.Description);
            iface.Property(s => s.Embedding);
        });

        // -- Object Types --

        builder.Object<AtomicNote>(obj =>
        {
            obj.Key(n => n.Id);
            obj.Property(n => n.CanonicalName).Required();
            obj.Property(n => n.Title).Required();
            obj.Property(n => n.Definition).Required();
            obj.Property(n => n.Category);
            obj.Property(n => n.Context);
            obj.Property(n => n.CreatedAt);
            obj.Property(n => n.ModifiedAt);

            // Links
            obj.ManyToMany<AtomicNote>("SemanticLinks")
                .WithEdge<KnowledgeLink>(edge =>
                {
                    edge.MapProperty(l => l.Type);
                    edge.MapProperty(l => l.Confidence);
                    edge.MapProperty(l => l.ContextDescription);
                });
            obj.HasMany<SourceReference>("Sources");

            // Actions
            obj.Action("Ingest")
                .Description("Ingest a source document into the knowledge graph")
                .Accepts<IngestRequest>()
                .Returns<IngestionResult>()
                .BoundToWorkflow("ingest-knowledge");

            obj.Action("Query")
                .Description("Query the knowledge graph for relevant concepts")
                .Accepts<KnowledgeQueryRequest>()
                .Returns<KnowledgeQueryResult>()
                .BoundToWorkflow("query-knowledge");

            // Events
            obj.Event<KnowledgeIngested>(evt =>
            {
                evt.Description("A source document was ingested, producing atomic notes");
                evt.MaterializesLink("Sources", e => e.SourceReferenceId);
            });

            obj.Event<SemanticLinkCreated>(evt =>
            {
                evt.Description("A semantic relationship between notes was identified");
                evt.MaterializesLink("SemanticLinks", e => e.TargetNoteId);
            });

            // Interface implementation
            obj.Implements<ISearchable>(map =>
            {
                map.Via(n => n.Title, s => s.Title);
                map.Via(n => n.Definition, s => s.Description);
                map.Via(n => n.Embedding, s => s.Embedding);
            });
        });

        builder.Object<SourceReference>(obj =>
        {
            obj.Key(s => s.Title);
            obj.Property(s => s.Author);
            obj.Property(s => s.Uri);
            obj.Property(s => s.RetrievedAt);
        });

        // -- Cross-Domain Links --

        builder.CrossDomainLink("KnowledgeInformsStrategy")
            .From<AtomicNote>()
            .ToExternal("trading", "Strategy")
            .ManyToMany()
            .WithEdge(edge =>
            {
                edge.Property<double>("Relevance");
                edge.Property<string>("Rationale");
            });

        builder.CrossDomainLink("KnowledgeInformsStyle")
            .From<AtomicNote>()
            .ToExternal("style-engine", "StyleCard")
            .ManyToMany()
            .WithEdge(edge =>
            {
                edge.Property<double>("Relevance");
            });
    }
}
```

---

## 15. Future Considerations

- **Object-level security policies:** Per-object type permission declarations in the ontology, enforced by ControlPlane policy engine
- **Ontology versioning:** Schema evolution with backward compatibility guarantees (additive changes safe, breaking changes detected at startup)
- **Ontology visualization:** Generate Mermaid/D3 diagrams from the `OntologyGraph` at runtime
- **Cost profiles on actions:** Budget metadata per action for scarcity-aware agent planning (§4.3 of workflow theory)
- **Streaming Object Sets:** Real-time Object Set subscriptions via Marten `ISubscription` for reactive agent patterns
- **Multi-tenant ontology isolation:** Per-tenant domain registration for SaaS scenarios
