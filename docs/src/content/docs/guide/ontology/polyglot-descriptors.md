---
title: Polyglot Descriptors
description: Register ontology descriptors that have no .NET CLR type.
sidebar:
  order: 4
---

The ontology layer originally keyed object types by their CLR `Type`. That breaks when the type lives in another runtime — a TypeScript service, a Python pipeline, or anything indexed via [SCIP](https://about.sourcegraph.com/blog/announcing-scip). Polyglot descriptors (2.5.0, #48) let an `ObjectTypeDescriptor` identify itself by `SymbolKey` instead of (or alongside) `ClrType`. This page covers the shape, when to use which field, and how `AONT037` catches missing identity at build time.

## The shape

`ObjectTypeDescriptor` lives in `Strategos.Ontology.Descriptors` and now carries four identity-related fields:

```csharp
public sealed record ObjectTypeDescriptor
{
    public required string Name { get; init; }
    public required string DomainName { get; init; }

    public Type?   ClrType   { get; init; }   // nullable for non-.NET descriptors
    public string? SymbolKey { get; init; }   // SCIP moniker
    public string? SymbolFqn { get; init; }   // language-formatted FQN; informational
    public string  LanguageId { get; init; } = "dotnet";

    public DescriptorSource Source     { get; init; } = DescriptorSource.HandAuthored;
    public string?          SourceId   { get; init; }
    public DateTimeOffset?  IngestedAt { get; init; }

    // ... existing Properties, Links, Actions, Events, Lifecycle ...
}
```

The construction invariant: at least one of `ClrType` and `SymbolKey` must be non-null. Setting `SymbolKey` while both fields are still null throws `InvalidOperationException`.

`SymbolKey` is the SCIP moniker — a stable, language-agnostic identifier. `LanguageId` carries the SCIP language tag (`"dotnet"`, `"typescript"`, `"python"`, …) and defaults to `"dotnet"` so existing hand-authored code compiles unchanged. `Source` is `HandAuthored` for `DomainOntology.Define()` contributions and `Ingested` when an `IOntologySource` produced the descriptor; `SourceId` and `IngestedAt` carry provenance for the ingested case.

## Which field do I set?

The hand-authored DSL `builder.Object<T>(...)` populates `ClrType` from the type parameter and leaves `SymbolKey` null. You only think about polyglot identity when:

1. Contributing descriptors through `IOntologySource` from a non-.NET runtime — set `SymbolKey` and `LanguageId`, leave `ClrType` null.
2. Using the descriptor-by-name overload `builder.ObjectType("Name", domainName: "...")` for a shape with no loaded .NET type — supply either a `Type` or a `symbolKey:` named argument.

The merge lattice: `ClrType` hand wins (falls back to ingested); `SymbolKey` ingested wins. A type appearing in both forms ends up with both fields populated and `Source = HandAuthored`.

## A polyglot example

Suppose a TypeScript service exports a `User` shape and you want it in the same ontology graph as your .NET trading types. The TypeScript side has no .NET assembly, so reach the graph through an `IOntologySource` and an `OntologyDelta.AddObjectType`:

```csharp
using System.Runtime.CompilerServices;
using Strategos.Ontology;
using Strategos.Ontology.Descriptors;

public sealed class TypeScriptUserSource : IOntologySource
{
    public string SourceId => "scip-typescript:identity-service";

    public async IAsyncEnumerable<OntologyDelta> LoadAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var descriptor = new ObjectTypeDescriptor
        {
            Name = "User",
            DomainName = "identity",
            SymbolKey = "scip-typescript npm identity-service 1.4.0 ./src/models/user.ts/User#",
            LanguageId = "typescript",
            Source = DescriptorSource.Ingested,
            SourceId = SourceId,
            IngestedAt = DateTimeOffset.UtcNow,
        };

        yield return new OntologyDelta.AddObjectType(descriptor)
        {
            SourceId = SourceId,
            Timestamp = DateTimeOffset.UtcNow,
        };

        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<OntologyDelta> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }
}
```

Register the source alongside your domains:

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();
    options.AddSource<TypeScriptUserSource>();
});
```

`OntologyGraphBuilder` drains every registered source's `LoadAsync` after hand-authored domains compile, so ingested deltas can reference existing hand descriptors. The `User` descriptor lands in the composed graph with `Source = Ingested` and `LanguageId = "typescript"`.

## AONT037: catching missing identity at build time

The descriptor-by-name overload can be called without supplying either a `Type` or a `symbolKey:` argument — illegal at runtime, easy to miss in review.

`AONT037 PolyglotInvariantViolated` is a Roslyn analyzer that scans `DomainOntology.Define` bodies for the descriptor-by-name overload and reports an `Error` when none of these are present: a `symbolKey:` named argument, a `clrType:` named argument, or a positional `typeof(...)` argument.

```csharp
// AONT037 fires — no identity supplied
builder.ObjectType("Foo", domainName: "trading");

// Clean — symbolKey supplied
builder.ObjectType("Foo", symbolKey: "scip-typescript ./mod#User", domainName: "trading");

// Clean — typeof() positional
builder.ObjectType("Foo", typeof(TradeOrder), "trading");

// Clean — generic overload carries the type parameter
builder.ObjectType<TradeOrder>();
```

The diagnostic stops the build before a descriptor that would throw at composition time can ship. If you want a `SymbolKey`-only descriptor, the analyzer message names both fix options.

## CLR-free path vs fluent CLR-generic surface

`ObjectTypeFromDescriptor` and `ApplyDelta` are the **first-class CLR-free path**. They accept a fully specified `ObjectTypeDescriptor` (identity by `SymbolKey`, `ClrType` left null) and are the seams `IOntologySource` uses to drain ingested types into the graph.

The fluent `Object<T>` / `Interface<T>` surface stays **CLR-generic**. Those overloads take a type parameter and populate `ClrType` from it. They are not a CLR-free authoring path, and there is no `Object(symbolKey)` fluent twin that also declares a polymorphic interface.

## CLR-free and polymorphic cannot combine

This is the CLR-free ⊕ polymorphic limit: you can have a CLR-free (SymbolKey-only) graph, or a polymorphic (interface-typed) fan-out, but not both on the same link. `RationaleCorpusParityTests` states the expressibility bound directly:

> a SymbolKey-ONLY interface fan-out is NOT expressible

An `InterfaceDescriptor` carries a CLR `Type`. A CLR-free descriptor has `ClrType == null`, so it cannot also be a polymorphic interface target. The parity corpus therefore splits into two dimensions that together cover the edge surface:

| Dimension | Identity | Shape | What it proves |
|---|---|---|---|
| A — polyglot | `SymbolKey` only (`ClrType == null`) | Monomorphic links | The CLR-free path (INV-8: identity by descriptor name / SymbolKey, never `typeof`) |
| B — polymorphic | CLR interface-typed (`Interface<T>`) | Per-descriptor junction fan-out | Relate routes to per-implementor tables; traverse `UNION ALL`-reads them |

A `SymbolKey`-only polymorphic interface is not a missing API to invent on the fluent surface — it is a type-system limit. Author CLR-free types through `ObjectTypeFromDescriptor` / `ApplyDelta` and keep them monomorphic; author polymorphic fan-out through `Interface<T>` and accept the CLR type.

## Where to go next

- [Getting Started](/strategos/guide/ontology/) — the hand-authored DSL.
- [Similarity Search](/strategos/guide/ontology/similarity-search/) — works against polyglot and CLR descriptors alike.
