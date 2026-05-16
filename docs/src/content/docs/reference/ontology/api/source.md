---
title: IOntologySource & Builder
sidebar:
  order: 6
---

Ontology graphs may be assembled from two contribution paths: hand-authored `DomainOntology.Define()` calls and external `IOntologySource` implementations that emit ontology events at startup. Both paths feed the same `OntologyGraphBuilder`, which composes their output, applies the DR-6 cross-provenance merge, and freezes a single `OntologyGraph`. The fluent DSL exposed during `Define()` is `IOntologyBuilder`.

Namespace: `Strategos.Ontology.Builder` (DSL) and `Strategos.Ontology` (source/delta types). Source: `src/Strategos.Ontology/Builder/IOntologyBuilder.cs`, `src/Strategos.Ontology/Sources/`.

## IOntologySource

Extension contract for ontology contributions from sources beyond hand-authored definitions. Registered via DI through `OntologyOptions.AddSource<T>()` and drained at startup by `OntologyGraphBuilder`.

| Member | Signature |
|---|---|
| `SourceId` | `string SourceId { get; }` |
| `LoadAsync` | `IAsyncEnumerable<OntologyDelta> LoadAsync(CancellationToken ct)` |
| `SubscribeAsync` | `IAsyncEnumerable<OntologyDelta> SubscribeAsync(CancellationToken ct)` |

`SourceId` is the stable identifier the source uses to tag provenance and conflict diagnostics — it appears on every `OntologyDelta` the source emits and threads through any composition exception that originates from this source. Source IDs must be unique within an ontology composition.

`LoadAsync` replays the source's full state as a stream of deltas. Called once at `OntologyGraphBuilder.Build()` time. Implementations stream their entire current view (object types, properties, links) as `OntologyDelta` values.

`SubscribeAsync` subscribes to incremental updates. Strategos 2.5.0 only consumes `LoadAsync` at startup; `SubscribeAsync` is part of the contract for forward compatibility with live invalidation (v2.6.0+). Static sources may return an immediately-completing async enumerable.

Implementations are registered as transient in DI — each `LoadAsync` drain instantiates a fresh source instance:

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();
    options.AddSource<MyExternalIngester>(); // implements IOntologySource
});
```

## OntologyDelta

The event vocabulary `IOntologySource` implementations emit. `OntologyDelta` is an abstract `record` with eight sealed-record variants covering object-type, property, and link granularity:

| Variant | Fields | Purpose |
|---|---|---|
| `AddObjectType` | `Descriptor: ObjectTypeDescriptor` | Adds a new object type to the graph. |
| `UpdateObjectType` | `Descriptor: ObjectTypeDescriptor` | Replaces the descriptor at `(DomainName, Name)`. |
| `RemoveObjectType` | `DomainName, TypeName: string` | Removes an object type by identity. |
| `AddProperty` | `DomainName, TypeName: string; Descriptor: PropertyDescriptor` | Appends a property to an existing type. |
| `RenameProperty` | `DomainName, TypeName, FromName, ToName: string` | Renames in place — preserves identity through the matcher. Never expanded into a remove/add pair. |
| `RemoveProperty` | `DomainName, TypeName, PropertyName: string` | Removes a property by name. |
| `AddLink` | `DomainName, SourceTypeName: string; Descriptor: LinkDescriptor` | Appends a link to a source type. |
| `RemoveLink` | `DomainName, SourceTypeName, LinkName: string` | Removes a link by name. |

Common fields on every delta:

| Field | Type | Notes |
|---|---|---|
| `SourceId` | `string` (required init) | Identifier of the emitting `IOntologySource`. |
| `Timestamp` | `DateTimeOffset` (required init) | Wall-clock instant of the originating change. |

Mechanical ingesters are forbidden from constructing `Add`/`Update` deltas whose descriptors contain `Actions`, `Events`, or `Lifecycle` — those are hand-authored intent. Validation runs at delta-apply time and surfaces as `AONT205` when violated.

## Provenance

Provenance is carried on `ObjectTypeDescriptor.Source` (and on each `PropertyDescriptor`/`LinkDescriptor`) as a `DescriptorSource` enum:

- `HandAuthored` — declared via the `DomainOntology` builder DSL.
- `Ingested` — emitted by an `IOntologySource` implementation; `SourceId` is also set.

When an `Add`/`Update` delta lands at a `(DomainName, Name)` slot already occupied by the opposite provenance, the builder folds the two descriptors through `MergeTwo.Merge`. Same-provenance collisions surface as `AONT040` duplicates downstream. Field-level provenance lets the freeze-time analyzers (`AONT201`–`AONT208`) compare hand-declared and ingested contributions independently.

## IOntologyBuilder

The fluent runtime DSL passed to `DomainOntology.Define(IOntologyBuilder builder)`. Implementations are internal; consumers interact through the interface.

| Member | Signature | Purpose |
|---|---|---|
| `Object<T>` | `void Object<T>(Action<IObjectTypeBuilder<T>> configure) where T : class` | Registers `T` with its default descriptor name (`typeof(T).Name`). |
| `Object<T>` (named) | `void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure) where T : class` | Registers `T` with an explicit descriptor name. Allows the same CLR type under multiple logical names; `name` must match `^[a-zA-Z_][a-zA-Z0-9_]*$`. |
| `Interface<T>` | `void Interface<T>(string name, Action<IInterfaceBuilder<T>> configure) where T : class` | Registers a polymorphic interface backed by the C# interface `T`. |
| `CrossDomainLink` | `ICrossDomainLinkBuilder CrossDomainLink(string name)` | Declares a relationship between types in different domain assemblies. |
| `ObjectTypeFromDescriptor` | `void ObjectTypeFromDescriptor(ObjectTypeDescriptor descriptor)` | Registers a fully-specified descriptor directly, bypassing the expression-tree DSL. Mechanism by which `IOntologySource` contributions reach the graph — ingested types may only be known by `SymbolKey` with no loaded CLR type. |
| `ApplyDelta` | `void ApplyDelta(OntologyDelta delta)` | Applies a delta to the current builder state. Dispatches by variant; `AddObjectType` routes to `ObjectTypeFromDescriptor`. Unknown variants throw `NotSupportedException`. |

Most consumers only call `Object`, `Interface`, and `CrossDomainLink` — `ObjectTypeFromDescriptor` and `ApplyDelta` are the seams the source-drain uses internally, exposed publicly so test wiring can replicate the drain.

## Related

- [Graph Versioning](/reference/ontology/graph-versioning/) — how the composed graph hashes to a deterministic `Version` once frozen.
- [Diagnostics: AONT200-series](/reference/diagnostics/) — drift diagnostics surfaced when hand-authored and ingested contributions disagree.
