---
title: MCP Integration
sidebar:
  order: 8
---

The 2.5.0 release upgrades `Strategos.Ontology.MCP` to MCP spec revision 2025-11-25. Tool descriptors now carry a human-readable title, a JSON Schema for the result shape, and a set of behavior hints; every tool response carries a `_meta` envelope that threads the ontology graph version through to the client. Together the changes let external agents like Cursor, Copilot, and Codex auto-approve the read-only ontology tools and invalidate their schema caches when the graph mutates.

## The descriptor shape

`OntologyToolDescriptor` keeps its 2.4-era `(Name, Description)` constructor and adds three init-only properties:

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

`Title` is the form an MCP client renders in its tool picker — "Explore Ontology Schema" rather than the wire name `ontology_explore`. `OutputSchema` is the result type's JSON Schema, generated at discovery time via `System.Text.Json.Schema.JsonSchemaExporter`. `Annotations` is a `ToolAnnotations` record carrying four MCP-spec hints.

The pre-2.5.0 two-arg constructor still compiles; the new properties default to safe values (no hints, no schema). Existing tests that match on `(Name, Description)` keep passing byte-for-byte.

## The annotation matrix

`ToolAnnotations` carries four booleans aligned with the MCP spec:

```csharp
public sealed record ToolAnnotations(
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);
```

`OntologyToolDiscovery.Discover()` populates them per tool:

| Tool | ReadOnly | Destructive | Idempotent | OpenWorld |
|---|:-:|:-:|:-:|:-:|
| `ontology_explore` | true | false | true | false |
| `ontology_query` | true | false | true | false |
| `ontology_action` | false | true | false | false |
| `ontology_validate` | true | false | true | false |

MCP clients that gate auto-approval on `ReadOnlyHint=true` browse the ontology and validate intent without prompting; `ontology_action` keeps the user in the loop because it carries `DestructiveHint=true`. `OpenWorldHint` stays false across the board — every tool's effects are bounded by the registered action set.

## The `_meta` envelope

Every result type carries a `Meta` property serialized as `_meta` on the wire:

```csharp
public sealed record ResponseMeta(
    [property: JsonPropertyName("ontologyVersion")] string OntologyVersion);
```

`OntologyVersion` is a hash digest prefixed with the algorithm — for instance `sha256:e3b0c4...`. The underlying `OntologyGraph.Version` property returns bare hex; `ResponseMeta.ForGraph(graph)` applies the prefix idempotently so the wire format is stable regardless of whose `JsonSerializerOptions` is in play. The MCP SDK uses `JsonSerializerDefaults.Web`; the `[JsonPropertyName("ontologyVersion")]` attribute pins the wire key against case-mangling.

Clients use the field to invalidate cached schema views when the graph mutates. Strategos 2.6.0 hybrid retrieval keys `IOntologyVersionedCache<TKey, TValue>` on this string; Exarchos's client-side schema cache compares the previous response's `_meta.ontologyVersion` against the fresh one and re-walks the schema on mismatch.

The shape is intentionally minimal in v1 — additional fields can land as init-only properties without breaking the wire contract. The 2.6.0 retrieval work will add `Hybrid`, `Reranked`, `CacheHit`, and a `Degraded` list per the retrieval design's compatibility model.

## A tool call round trip

An agent calls `ontology_explore` for the `trading` domain. The request reaches `OntologyExploreTool.Explore`, which queries the graph and returns:

```csharp
return new ExploreResult(
    scope,
    items,
    ResponseMeta.ForGraph(_graph));
```

The serialized response carries the result fields and a `_meta` envelope:

```json
{
  "scope": "domain:trading",
  "items": [
    { "name": "Order", "kind": "ObjectType" },
    { "name": "Position", "kind": "ObjectType" }
  ],
  "_meta": {
    "ontologyVersion": "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
  }
}
```

The client caches the result keyed on the version string. On the next call, the version stamp tells the client whether to reuse the cached schema or refetch.

`OntologyServerCapabilitiesProvider` exposes the same string at initialize time so an MCP host can populate `capabilities._meta.ontologyVersion` in its `initialize` response without poking at the graph directly. The compatibility model is additive end to end — clients that ignore the envelope keep working against the legacy descriptor surface, while clients that read it gain deterministic cache invalidation.
