---
title: Ontology Sources
sidebar:
  order: 5
---

`IOntologySource` is the 2.5.0 extension point for contributing ontology descriptors from somewhere other than hand-authored `DomainOntology.Define()` code. A source emits a stream of `OntologyDelta` values; `OntologyGraphBuilder` drains each one at startup and folds the deltas into the same per-domain builders the hand-authored pass populated. The result is one unified graph that downstream consumers query without caring which side contributed which descriptor.

## When to implement a source

Reach for `IOntologySource` when the schema lives outside C#. A few examples:

- A Roslyn-driven ingester that walks another assembly and emits descriptors it never loaded as runtime types.
- A JSON or YAML schema file the application reads at boot.
- A live system-of-record whose schema changes between deploys.

If the schema is hand-authored alongside the rest of the application, prefer `DomainOntology.Define()` directly. Sources are for descriptors that cannot be expressed as expression-tree DSL because the CLR type is not loaded — only the `SymbolKey` is known.

## The interface

```csharp
public interface IOntologySource
{
    string SourceId { get; }
    IAsyncEnumerable<OntologyDelta> LoadAsync(CancellationToken ct);
    IAsyncEnumerable<OntologyDelta> SubscribeAsync(CancellationToken ct);
}
```

`SourceId` tags every delta and propagates into composition diagnostics so a conflict can be attributed back to the source that emitted the contribution. `LoadAsync` replays the full state once at `OntologyGraphBuilder.Build()` time. `SubscribeAsync` reserves room for incremental updates in 2.6.0 and later; static sources may return an empty sequence.

## The delta vocabulary

`OntologyDelta` is an abstract record with eight sealed variants. The `AddObjectType` and `UpdateObjectType` variants carry an `ObjectTypeDescriptor`; the property and link variants carry the narrower `(DomainName, TypeName, …)` shape. `RenameProperty` is a single delta — never a `RemoveProperty` + `AddProperty` pair — so the rename matcher can preserve identity through the change.

Every delta carries `SourceId` and `Timestamp`, both required init properties. The `IOntologyBuilder.ApplyDelta` dispatcher routes by variant; unknown types throw `NotSupportedException` so adding a future variant fails loudly rather than silently dropping.

## Provenance metadata

Each descriptor carries a `DescriptorSource` enum on its `Source` property:

- `DescriptorSource.HandAuthored` — produced by `DomainOntology.Define()`.
- `DescriptorSource.Ingested` — produced by a source.

When a hand-authored descriptor and an ingested descriptor land on the same `(DomainName, Name)`, the builder folds them through `MergeTwo.Merge` so neither side silently overwrites the other. Same-provenance collisions still surface as `AONT040` duplicates downstream. The merge keeps a pre-merge snapshot of the ingested side so AONT200-series graph-freeze diagnostics can diff the hand-authored property set against the ingested one without losing provenance.

`Source` also gates `AONT205`: an ingested descriptor must leave `Actions`, `Events`, and `Lifecycle` empty. Those are intent-only fields — a mechanical ingester has no business contributing them.

## Runtime registration

Sources register through DI:

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingDomain>();
    options.AddSource<JsonSchemaSource>();
});
```

`OntologyOptions.AddSource<T>` requires a parameterless constructor, registers the source as transient in the container, and queues a factory the graph builder calls at compose time. The transient lifetime matches the contract on `IOntologySource`: each `LoadAsync` drain creates a fresh source instance.

If you need to register a source without DI — most often in tests — call `graphBuilder.AddSources(myEnumerable)` directly. The drain treats both registration paths identically.

## A concrete example

This source loads object-type definitions from a JSON file and emits an `AddObjectType` delta per entry. Each descriptor carries the source's `SourceId` so a composition diagnostic can later attribute the contribution back to this file:

```csharp
public sealed class JsonSchemaSource : IOntologySource
{
    public string SourceId => "json-schema:product-catalog.json";

    public async IAsyncEnumerable<OntologyDelta> LoadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "product-catalog.json");
        await using var stream = File.OpenRead(path);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var descriptor = new ObjectTypeDescriptor
            {
                DomainName = entry.GetProperty("domain").GetString()!,
                Name = entry.GetProperty("name").GetString()!,
                SymbolKey = entry.GetProperty("symbolKey").GetString(),
                Source = DescriptorSource.Ingested,
                SourceId = SourceId,
                Properties = ReadProperties(entry),
            };

            yield return new OntologyDelta.AddObjectType(descriptor)
            {
                SourceId = SourceId,
                Timestamp = DateTimeOffset.UtcNow,
            };
        }
    }

    public IAsyncEnumerable<OntologyDelta> SubscribeAsync(CancellationToken ct)
        => AsyncEnumerable.Empty<OntologyDelta>();
}
```

The descriptor carries `SymbolKey` rather than `ClrType` because the underlying type may not be loaded; the DR-1 identity invariant requires at least one of the two. Provenance metadata (`Source = Ingested`, `SourceId = …`) flows through the merge and into any later `OntologyCompositionException`, so a conflict diagnostic always names the originating source.
