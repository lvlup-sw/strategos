# Design: MCP Surface Conformance — Tool Descriptor Upgrade + Graph Versioning

**Date:** 2026-04-19
**Status:** Draft (ideate output)
**Workflow:** `mcp-surface-conformance` (feature, ideate phase)
**Milestone:** Ontology 2.5.0 — Coordination Floor (Slice A)
**Closes:** strategos#40, strategos#44
**Parent ADR:** `docs/reference/2026-04-18-exarchos-basileus-coordination.md` §§2.11, 2.12
**Related (downstream):** strategos#47 (2.6.0 hybrid retrieval — depends on `_meta.ontologyVersion` threading and tool annotations)

---

## 1. Context and thesis

The Exarchos↔Basileus coordination ADR commits Strategos's `Strategos.Ontology.MCP` package to MCP 2025-11-25 spec conformance and ontology graph versioning. Today the tool descriptor carries only `(Name, Description)`, the `OntologyGraph` has no version identifier, and tool responses carry no `_meta` envelope. Three downstream consumers depend on these gaps closing before they can ship:

1. **External MCP clients (Cursor, Copilot, Codex).** Without `ToolAnnotations.ReadOnlyHint=true`, every `ontology_explore` / `ontology_query` call prompts the user. With it, the read-only tools auto-approve and the agent can browse the ontology freely. ADR §2.11 names this as a load-bearing UX tax.
2. **Strategos 2.6.0 hybrid retrieval (#47).** The retrieval composition design depends on `_meta.ontologyVersion` threading to invalidate `IOntologyVersionedCache<TKey, TValue>` entries on graph mutation. Without the version signal at the response boundary, the cache cannot enforce the ADR §2.12 stale-guard invariant.
3. **Exarchos schema cache (ADR §2.8 addition).** Exarchos caches `ontology_explore` results client-side; the resolver compares cached version against fresh responses. No version → no cache invalidation → silent drift.

This design lands both pieces in a single cycle because they share the same surface — the `OntologyToolDescriptor` and the response envelope shape — and because the integrating glue (`_meta.ontologyVersion` on every response) only makes sense when both the version and the descriptor upgrade exist.

**Non-goals.**
- No new tools. `ontology_validate` lands with strategos#41 (Slice C), `fabric_resolve` and `intent_register` are Basileus-side.
- No source-generator changes (annotations are static per-tool, not derived from DSL declarations in v1).
- No MCP server runtime changes — Strategos ships descriptors and tool classes; the host (Basileus AgentHost or test harness) registers them with its MCP server. This design constrains the descriptor + result shape, not the registration mechanism.

## 2. Scope

**In scope.**

- `OntologyGraph.Version` — content-stable sha256 hash computed at build time from the graph's structural fields.
- `OntologyToolDescriptor` upgrade — add `Title`, `OutputSchema`, `Annotations` per MCP 2025-11-25.
- `ToolAnnotations` record — `ReadOnlyHint`, `DestructiveHint`, `IdempotentHint`, `OpenWorldHint` (booleans, MCP-spec aligned).
- Annotation matrix wired into `OntologyToolDiscovery.Discover()` for the three currently-shipping tools (explore, query, action).
- `_meta` envelope on every tool response — `ExploreResult`, `QueryResult`, `SemanticQueryResult`, `ActionToolResult`. Carries `ontologyVersion` at minimum; design space leaves room for future fields.
- Output schema generation — derive `OutputSchema` JsonElement from each result type via System.Text.Json.Schema (built into .NET 10).

**Out of scope (explicit).**

- Hot-reload notifications (`notifications/resources/updated` for `ontology://{domain}/*`) — ADR §2.12 calls this Gap 8, deferred to a follow-up issue.
- The `ontology_validate` tool descriptor and annotations — lands with strategos#41 (Slice C).
- Basileus-side tool descriptors (`fabric_resolve`, `intent_register`) — owned by the basileus repo per ADR §2.2.
- Cryptographic signing of `Version` (ADR §9.5 open question).

---

## 3. Architecture overview

```
┌─────────────────────────────────────────────────────────────┐
│  Strategos.Ontology  (build-time)                           │
│    OntologyGraph                                            │
│      └── Version : string  (NEW — sha256 over structural)   │
│                                                             │
│  Strategos.Ontology.MCP  (runtime)                          │
│    OntologyToolDescriptor  (UPGRADED)                       │
│      ├── Name                                               │
│      ├── Title         (NEW — human-readable)               │
│      ├── Description                                        │
│      ├── OutputSchema  (NEW — JsonElement?)                 │
│      ├── Annotations   (NEW — ToolAnnotations)              │
│      └── ConstraintSummaries                                │
│                                                             │
│    ToolAnnotations  (NEW)                                   │
│      ├── ReadOnlyHint                                       │
│      ├── DestructiveHint                                    │
│      ├── IdempotentHint                                     │
│      └── OpenWorldHint                                      │
│                                                             │
│    OntologyToolDiscovery.Discover()                         │
│      └── populates Title + OutputSchema + Annotations       │
│         per the per-tool matrix (§5)                        │
│                                                             │
│    Tool responses (UPGRADED — gain Meta envelope)           │
│      ExploreResult(Scope, Items, Meta)                      │
│      QueryResult(ObjectType, Items, ..., Meta)              │
│      SemanticQueryResult(..., Meta)                         │
│      ActionToolResult(..., Meta)                            │
│                                                             │
│    ResponseMeta  (NEW)                                      │
│      └── OntologyVersion : string                           │
│         (extensible; future fields per §6)                  │
└─────────────────────────────────────────────────────────────┘
```

All four tools (`OntologyExploreTool`, `OntologyQueryTool`, `OntologyActionTool`, plus the future `OntologyValidateTool`) become `OntologyGraph`-aware for one purpose only: stamping `Meta.OntologyVersion = _graph.Version` into every result. The graph reference already exists in each tool's constructor, so this is a one-line change per tool.

---

## 4. Decisions

### 4.1 `OntologyGraph.Version` — what's hashed and how

```csharp
public sealed class OntologyGraph
{
    // existing members...

    /// <summary>
    /// SHA-256 of a stable serialization of the graph's structural fields.
    /// Computed at construction; identical DSL produces identical hash across
    /// processes and machines. Surfaced in MCP responses as _meta.ontologyVersion
    /// so consumers can invalidate cached schema views on mismatch.
    /// </summary>
    public string Version { get; }
}
```

**What's hashed (in this order, alphabetized within each list for stability):**

1. `Domains` — `(DomainName, ObjectTypes.Count)` per domain.
2. `ObjectTypes` — for each, `(DomainName, Name, ParentTypeName?)` plus:
   - `Properties` sorted by `Name`: `(Name, Kind, ClrType.FullName, IsNullable, VectorDimensions?)`
   - `Actions` sorted by `Name`: `(Name, AcceptsType?.FullName, ReturnsType?.FullName, BindingType, IsReadOnly?, Preconditions sorted by Description, Postconditions sorted by Description)`
   - `Links` sorted by `Name`: `(Name, TargetTypeName, Cardinality, EdgeProperties sorted by Name)`
   - `Events` sorted by `EventType.FullName`: `(EventType.FullName, Severity, MaterializedLinks sorted, UpdatedProperties sorted)`
   - `Lifecycle` (if any): states sorted, transitions sorted by `(From, To, Trigger)`
   - `ImplementedInterfaces` sorted by `Name`
3. `Interfaces` sorted by `Name`: `(Name, Properties sorted by Name)`
4. `CrossDomainLinks` sorted by `(SourceDomain, SourceType, LinkName)`: full link descriptor
5. `WorkflowChains` sorted by `WorkflowName`: `(WorkflowName, ConsumedType.FullName, ProducedType.FullName)`

Hash representation: lowercase hex string of sha256 (64 chars). Prefixed with `"sha256:"` when emitted in `_meta` to leave room for future hash-algorithm migration.

**What's deliberately NOT hashed:**
- `Warnings` (advisory, non-structural)
- `ObjectTypeNamesByType` (derived index, not source)
- Action / property `Description` text (free-form documentation; changing prose should not bust caches that exist for *structural* invalidation)

The choice to exclude `Description` from the hash is load-bearing: documentation churn would otherwise drive constant cache invalidation in Exarchos and Basileus without changing the schema agents reason about. If we later want a "documentation version" for a separate cache, it can be a sibling property (`DocumentationVersion`).

**Where computed.** Inside the `OntologyGraph` constructor (after lookups are built), via a `static string ComputeVersion(...)` helper in a new `OntologyGraphHasher.cs`. The hasher is internal; only `OntologyGraph.Version` is public.

### 4.2 `ToolAnnotations` record

```csharp
namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP 2025-11-25 tool annotations. Booleans are hints to the client about
/// the tool's behavior so the client can gate auto-approval, batching, and
/// caching decisions. Authoritative reference: MCP spec, server/tools.
/// </summary>
public sealed record ToolAnnotations(
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);
```

Pure data record. Lives alongside `OntologyToolDescriptor` in `Strategos.Ontology.MCP/`. No runtime behavior — the values are consumed by whoever serializes the descriptor for MCP transport.

### 4.3 `OntologyToolDescriptor` upgrade

```csharp
public sealed record OntologyToolDescriptor(
    string Name,
    string Description)
{
    public string? Title { get; init; }
    public JsonElement? OutputSchema { get; init; }
    public ToolAnnotations Annotations { get; init; } =
        new(ReadOnlyHint: false, DestructiveHint: false,
            IdempotentHint: false, OpenWorldHint: false);
    public IReadOnlyList<ActionConstraintSummary> ConstraintSummaries { get; init; } = [];
}
```

**Backward-compatibility:**
- Existing two-arg constructor unchanged.
- `Title`, `OutputSchema` default to `null`.
- `Annotations` defaults to all-false (the MCP-spec safe default — "no hints, ask the user").
- `ConstraintSummaries` unchanged.
- Existing tests that assert `(Name, Description)` shape pass byte-for-byte.

### 4.4 Annotation matrix (per ADR §2.11)

```csharp
// Inside OntologyToolDiscovery.Discover():

new OntologyToolDescriptor("ontology_explore", BuildExploreDescription(...))
{
    Title = "Explore Ontology Schema",
    OutputSchema = JsonSchemaFor<ExploreResult>(),
    Annotations = new(
        ReadOnlyHint: true, DestructiveHint: false,
        IdempotentHint: true, OpenWorldHint: false),
}

new OntologyToolDescriptor("ontology_query", BuildQueryDescription(...))
{
    Title = "Query Ontology Objects",
    OutputSchema = JsonSchemaFor<QueryResult>(),  // see §4.6 on union shape
    Annotations = new(
        ReadOnlyHint: true, DestructiveHint: false,
        IdempotentHint: true, OpenWorldHint: false),
}

new OntologyToolDescriptor("ontology_action", BuildActionDescription(...))
{
    Title = "Execute Ontology Action",
    OutputSchema = JsonSchemaFor<ActionToolResult>(),
    Annotations = new(
        ReadOnlyHint: false, DestructiveHint: true,
        IdempotentHint: false, OpenWorldHint: false),
    ConstraintSummaries = constraintSummaries,
}
```

The Title strings are hand-curated for the three current tools; not derived from `Name` because the spec's intent for `title` is "human-readable display label" (Cursor renders this in its tool picker, for example), and "ontology explore" reads worse than "Explore Ontology Schema."

### 4.5 Response `Meta` envelope

```csharp
namespace Strategos.Ontology.MCP;

/// <summary>
/// Per-response metadata threaded through every ontology MCP tool result.
/// Consumers use OntologyVersion to invalidate schema caches when the
/// ontology graph mutates (Strategos 2.6.0 hybrid retrieval cache;
/// Exarchos client-side schema cache).
/// </summary>
public sealed record ResponseMeta(string OntologyVersion);

// Result types upgraded to carry Meta. ExploreResult example:
public sealed record ExploreResult(
    string Scope,
    IReadOnlyList<Dictionary<string, object?>> Items,
    ResponseMeta Meta);
```

**Backward-compatibility considered.** The result records' constructors gain a required `Meta` parameter. This is a breaking change for any caller constructing the records directly. Mitigation:

1. Strategos's `Strategos.Ontology.MCP` package is at version 2.4.x — this is a 2.5.0 minor version cut, where breaking changes to internal-to-the-package types are acceptable. The public API surface most consumers depend on is the *tool method* (`Explore(...)`, `Query(...)`), not the result record's constructor.
2. Tests in `Strategos.Ontology.MCP.Tests` will need updates to pass `Meta` — bounded in this design's scope, not a downstream cost.
3. Basileus consumes the `Strategos.Ontology.MCP` package; its construction sites are minimal (it invokes the tools, doesn't construct the result records). Verified by grep on the Basileus design docs.

If the breaking change is rejected during plan review, the fallback is an additive `Meta` property with `init`-only semantics and a default value computed from a global static (passed via `OntologyToolDiscovery` at construction). That fallback adds a hidden coupling and is rejected from the v1 design — the constructor parameter is the honest shape.

### 4.6 OutputSchema generation

.NET 10 ships `System.Text.Json.Schema.JsonSchemaExporter.GetJsonSchemaAsNode(JsonSerializerOptions, Type)`. This produces a JSON-Schema-spec `JsonNode` for any serializable type. The MCP spec accepts JSON Schema for `outputSchema`.

```csharp
private static JsonElement JsonSchemaFor<T>()
{
    var node = JsonSchemaExporter.GetJsonSchemaAsNode(
        JsonSerializerOptions.Default, typeof(T));
    return JsonSerializer.SerializeToElement(node);
}
```

**Special case — `QueryResult` / `SemanticQueryResult`.** `OntologyQueryTool` returns either a `QueryResult` (filter / link / interface query) or a `SemanticQueryResult` (semantic search). Two options:

- **Option A:** Two separate tool descriptors (`ontology_query` and `ontology_semantic_query`). Cleaner schemas; breaking change to the tool-name surface.
- **Option B:** One `ontology_query` descriptor with a `oneOf` schema spanning both result shapes. Preserves the existing tool name; produces a slightly noisier schema.

**Recommendation: Option B.** ADR §2.2's tool table lists `ontology_query` as a single tool with semantic search baked in (parameter-driven, not separate tool). The `oneOf` schema is mechanical to generate via `JsonSchemaExporter` over a `[JsonDerivedType]`-decorated base record; runtime consumers (Cursor, Copilot) handle `oneOf` in tool outputs since the MCP spec endorses it.

### 4.7 Tool wiring — graph propagation

Each tool already takes `OntologyGraph` in its constructor. Stamping the version into results is a one-line change per tool:

```csharp
// OntologyExploreTool.Explore(...) — last line before each return:
return new ExploreResult(scope, items, new ResponseMeta(_graph.Version));
```

No new abstractions, no helper class. The `_graph.Version` access is O(1) (computed once at graph construction).

### 4.8 Discovery enhancement — surface Version on initialize-style metadata

`OntologyToolDiscovery.Discover()` already returns the descriptor list. Add a sibling method:

```csharp
public OntologyServerCapabilities GetServerCapabilities() =>
    new(OntologyVersion: _graph.Version);

public sealed record OntologyServerCapabilities(string OntologyVersion);
```

This is the affordance MCP-server hosts (Basileus AgentHost) consume to populate the `initialize` response's `capabilities._meta.ontologyVersion` field. Strategos ships the data; the host wires it into its MCP transport.

---

## 5. Annotation matrix (locked for v1)

Reproduced from ADR §2.11 with v1 scope marked. Future tools' annotations are listed but not implemented in this slice.

| Tool | readOnly | destructive | idempotent | openWorld | Ships in this slice |
|---|:-:|:-:|:-:|:-:|:-:|
| `ontology_explore` | ✓ | | ✓ | | ✓ |
| `ontology_query` | ✓ | | ✓ | | ✓ |
| `ontology_action` | | ✓ | | | ✓ |
| `ontology_validate` | ✓ | | ✓ | | (lands with #41) |
| `fabric_resolve` | ✓ | | ✓ | | (Basileus) |
| `intent_register` | | ✓ | | ✓ | (Basileus) |

Rationale per ADR §2.11: read-only + idempotent tools auto-approve in clients that gate on these hints. `ontology_action` is destructive (it dispatches actions with postconditions) but not openWorld (effects are bounded by the registered action set).

---

## 6. Extensibility considerations

The `_meta` envelope is intentionally minimal in v1 (`OntologyVersion` only). Two near-term extensions are anticipated by downstream work:

- **Slice C (#41 `ontology_validate`)** will add a `BlastRadius` summary or a per-result `ValidationVerdict` reference — those land as additive `init`-only properties on `ResponseMeta`.
- **2.6.0 (#47 hybrid retrieval)** will add `Hybrid: bool`, `Reranked: bool`, `CacheHit: bool`, `Degraded: string[]` per the retrieval design's `_meta` shape — also additive.

The v1 design uses a record so additive properties are non-breaking. We do not pre-add the future fields; they ship with the work that needs them.

---

## 7. Test plan

Net new tests (TUnit, in `Strategos.Ontology.Tests` and `Strategos.Ontology.MCP.Tests`):

**`OntologyGraph.Version` tests (Strategos.Ontology.Tests/OntologyGraphVersionTests.cs):**
- Stable: same DSL → same Version across process invocations
- Sensitive: adding a property changes Version
- Sensitive: renaming an action changes Version
- Insensitive: changing a Description does NOT change Version
- Insensitive: changing Warnings does NOT change Version
- Format: starts with no prefix in property; the `"sha256:"` prefix is added at emission time
- Hex: 64 lowercase hex chars

**`ToolAnnotations` + `OntologyToolDescriptor` upgrade tests (Strategos.Ontology.MCP.Tests/OntologyToolDescriptorTests.cs):**
- Default annotations are all-false on the two-arg constructor
- Init-only `Title`, `OutputSchema`, `Annotations` round-trip via record `with`
- Backward compat: old `(Name, Description)` construction still compiles and matches existing tests

**`OntologyToolDiscovery` annotation matrix tests (Strategos.Ontology.MCP.Tests/OntologyToolDiscoveryAnnotationTests.cs):**
- `ontology_explore` discovered with `ReadOnlyHint=true, IdempotentHint=true`
- `ontology_query` discovered with `ReadOnlyHint=true, IdempotentHint=true`
- `ontology_action` discovered with `DestructiveHint=true`
- All three carry non-null `Title` and non-null `OutputSchema`

**Response `Meta` envelope tests (Strategos.Ontology.MCP.Tests/OntologyExploreToolMetaTests.cs + sibling files for query/action):**
- Every result type carries `Meta.OntologyVersion` populated from the graph
- Meta.OntologyVersion equals `graph.Version` (no formatting drift)

**Cross-cutting tests:**
- A reference DSL fixture (small two-domain graph) produces a known Version hash committed as a test constant — guards against accidental hashing-algorithm drift across .NET versions.

---

## 8. Migration / rollout

1. **Strategos 2.5.0 release** ships the upgraded `OntologyToolDescriptor`, `ToolAnnotations`, `OntologyGraph.Version`, and the result `Meta` envelope.
2. **Basileus** picks up the package on its next dependency refresh; its tool registration code consumes `Annotations` when populating its MCP server's tool list. No-op for the AgentHost wiring beyond the package bump until 2.6.0 (#47) actually consumes the `_meta.ontologyVersion`.
3. **Exarchos** picks up the version signal once `exarchos_sync` proxies start passing through `_meta.ontologyVersion` (ADR §6.3 NEW item: "Ontology cache + version invalidation"). That's exarchos-side work, separate from this design.
4. **Internal Strategos consumers** — the only one is `Strategos.Ontology.MCP` itself. Tests update; no other call sites construct `ExploreResult` / `QueryResult` directly outside this package.

---

## 9. Cross-repo implementation map

### Strategos (this slice)

| Issue | Component | Files |
|---|---|---|
| #44 | `OntologyGraph.Version` + hasher | `src/Strategos.Ontology/OntologyGraph.cs`, `src/Strategos.Ontology/Internal/OntologyGraphHasher.cs` (new) |
| #44 | Version tests | `src/Strategos.Ontology.Tests/OntologyGraphVersionTests.cs` (new) |
| #40 | `ToolAnnotations` record | `src/Strategos.Ontology.MCP/ToolAnnotations.cs` (new) |
| #40 | `OntologyToolDescriptor` upgrade | `src/Strategos.Ontology.MCP/OntologyToolDescriptor.cs` |
| #40 | Annotation matrix wiring | `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs` |
| #40 | `JsonSchemaFor<T>` helper | `src/Strategos.Ontology.MCP/Internal/JsonSchemaHelper.cs` (new) |
| both | `ResponseMeta` record | `src/Strategos.Ontology.MCP/ResponseMeta.cs` (new) |
| both | Result records gain `Meta` | `ExploreResult.cs`, `QueryResult.cs`, `SemanticQueryResult.cs`, `ActionToolResult.cs` |
| both | Tool stamping calls | `OntologyExploreTool.cs`, `OntologyQueryTool.cs`, `OntologyActionTool.cs` |
| #44 | `OntologyServerCapabilities` discovery | `src/Strategos.Ontology.MCP/OntologyToolDiscovery.cs` |
| both | Descriptor + annotation tests | `src/Strategos.Ontology.MCP.Tests/OntologyToolDescriptorTests.cs` (new), `OntologyToolDiscoveryAnnotationTests.cs` (new) |
| both | Meta envelope tests | `OntologyExploreToolMetaTests.cs`, `OntologyQueryToolMetaTests.cs`, `OntologyActionToolMetaTests.cs` (all new) |

No changes to Strategos.Contracts in this slice — `ResponseMeta` is a Strategos-internal envelope; cross-repo TypeSpec ingestion lands with #41/#47 when the contract surface grows.

### Basileus / Exarchos

No work in this slice. Consumed downstream:
- Basileus picks up the package on its next refresh; surfaces `Annotations` when registering Strategos tools with its MCP server.
- Exarchos consumes `_meta.ontologyVersion` from `exarchos_sync` proxy responses for cache invalidation (separate exarchos issue, ADR §6.3).

---

## 10. Consequences

### Positive

- **Generic-runtime parity unblocked.** Cursor, Copilot, Codex, OpenCode receive the `ReadOnlyHint=true` annotation on the read-only ontology tools, which lifts every per-call user prompt for browsing and querying. Material UX improvement on day one.
- **2.6.0 unblocked.** `_meta.ontologyVersion` is the load-bearing signal `IOntologyVersionedCache<TKey, TValue>` (basileus#167) keys on. Without it, the hybrid retrieval cache cannot ship its stale-guard invariant.
- **Self-describing tool surface.** `OutputSchema` lets external agents validate responses and author follow-up queries deterministically — the same value MCP tool annotations were designed to deliver.
- **One coherent cycle.** Both issues share the descriptor + response shape; landing them together minimizes review thrash and downstream consumer churn.

### Negative / costs

- **Breaking change to internal result record constructors.** `ExploreResult`, `QueryResult`, `SemanticQueryResult`, `ActionToolResult` gain a required `Meta` parameter. Mitigated by §4.5 — the breakage is bounded to Strategos.Ontology.MCP's own tests; no external consumers construct these directly.
- **One-time hash-algorithm decision.** SHA-256 is the right v1 default (collision-resistant, fast, ubiquitous). The `"sha256:"` prefix in the wire format leaves room for future migration; the `OntologyGraph.Version` property does not (it returns the hex). If we ever need pluggable hashing, that's a v2.
- **JSON-schema generation cost at startup.** `JsonSchemaExporter` runs once per tool discovery. ~1ms per type on the reference shapes; not in any hot path.
- **Trim/AOT propagation through `OntologyToolDiscovery.Discover()`.** `JsonSchemaExporter` (used to populate each tool's `OutputSchema`) is reflection-based, so `JsonSchemaHelper.JsonSchemaFor<T>()` carries `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]`. These annotations propagate to `OntologyToolDiscovery.Discover()` and the three private descriptor builders. Downstream consumers (Basileus AgentHost, custom MCP host integrations) calling `Discover()` from trim/AOT-published projects will see IL2026 / IL3050 analyzer warnings they must suppress at their call site or via `<TrimmerRootDescriptor>` config. The package itself remains `<IsAotCompatible>true</IsAotCompatible>` — the constraint is on the schema-generation surface, not the package's other code paths. Future work: precompute schemas at package-build time via a source generator (deferred; out of scope for 2.5.0).

### Neutral

- **`Title` is hand-curated for three tools.** Future tools must remember to set it; the alternative (deriving from `Name`) reads worse for humans and the MCP spec's intent for `title` is the human-readable form. No analyzer enforces presence in v1.
- **`Description` excluded from the Version hash.** Documented as a deliberate choice in §4.1; if this turns out to surprise consumers in practice, a sibling `DocumentationVersion` is the path forward.

---

## 11. Open questions

1. **`OutputSchema` for `QueryResult` — option A or B?** Recommendation in §4.6 is option B (`oneOf` schema spanning `QueryResult` and `SemanticQueryResult`). Confirm during plan review; option A (split tool name) is the reversal cost if the `oneOf` proves problematic for downstream clients.
2. **Should `OntologyServerCapabilities` live in `Strategos.Ontology.MCP` or a new `Strategos.Ontology.MCP.Server` package?** v1 places it alongside `OntologyToolDiscovery` for proximity; if Basileus's host code ends up needing additional capability surfaces, a dedicated server-facing package may be warranted in v2.
3. **`_meta` field naming convention — `Meta` or `_meta`?** C# property is `Meta` (PascalCase per .NET conventions); JSON serialization configurable. The MCP spec emits `_meta` on the wire; the System.Text.Json `[JsonPropertyName("_meta")]` attribute on the property handles the wire-format mapping. Confirm during implementation.
4. **Test fixture for the cross-cutting Version hash constant.** A small two-domain DSL committed to tests pins the hash output. If future .NET versions change `JsonSchemaExporter` output or the Span-based hashing surface, the test surfaces the drift loudly. Acceptable maintenance cost; flag if it becomes flaky.

---

## 12. Related

- ADR: `docs/reference/2026-04-18-exarchos-basileus-coordination.md` §§2.11, 2.12
- Data-shape research: `docs/reference/2026-04-19-data-shape-query-performance-relevance.md` §3.4 (ontology versioning as cache-key glue)
- Retrieval composition: `docs/reference/2026-04-19-retrieval-composition-for-ontology-mcp.md` §5.4 (`IOntologyVersionedCache<TKey, TValue>` consumes this design's output)
- Issue #44: Ontology graph versioning
- Issue #40: MCP 2025-11-25 conformance
- MCP spec: [tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools), [tool annotations](https://blog.modelcontextprotocol.io/posts/2026-03-16-tool-annotations/)
- .NET 10: [`System.Text.Json.Schema.JsonSchemaExporter`](https://learn.microsoft.com/dotnet/api/system.text.json.schema.jsonschemaexporter)
