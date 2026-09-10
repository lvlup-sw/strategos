---
title: Getting Started with Ontology
description: Define type-safe domain entities and query them at runtime.
sidebar:
  order: 1
---

The ontology layer maps your existing C# types into a queryable semantic graph of object types, properties, links, actions, and events. Definitions are validated at compile time by a Roslyn source generator; running code asks the same graph for valid actions, computed-property dependencies, and lifecycle state through `IOntologyQuery`.

This page walks through the three steps you take for any new ontology: define a `DomainOntology`, register it with `AddOntology`, and query it from a service or step.

## 1. Define a domain

A domain is a class that derives from `DomainOntology` and overrides `Define(IOntologyBuilder builder)`. Inside `Define`, you call `builder.Object<T>(...)` for each CLR type you want to expose, set its key, and declare its properties, links, actions, and lifecycle.

```csharp
using Strategos.Ontology;
using Strategos.Ontology.Builder;

public sealed record Position
{
    public Guid Id { get; init; }
    public string Symbol { get; init; } = "";
    public int Quantity { get; init; }
    public decimal AverageCost { get; init; }
    public decimal UnrealizedPnL { get; init; }
    public PositionStatus Status { get; init; }
}

public sealed class TradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Position>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.UnrealizedPnL).Computed()
               .DerivedFrom(p => p.Quantity, p => p.AverageCost);

            obj.HasMany<TradeOrder>("Orders").Inverse("Position");

            obj.Action("ExecuteTrade")
               .Requires(p => p.Status == PositionStatus.Active)
               .Modifies(p => p.Quantity)
               .CreatesLinked<TradeOrder>("Orders");

            obj.Lifecycle<PositionStatus>(p => p.Status, lc =>
            {
                lc.State(PositionStatus.Pending).Initial();
                lc.State(PositionStatus.Active);
                lc.State(PositionStatus.Closed).Terminal();
                lc.Transition(PositionStatus.Pending, PositionStatus.Active)
                  .TriggeredByAction("Activate");
            });
        });
    }
}
```

The expression-tree DSL is validated by the source generator. Diagnostics in the AONT001–AONT099 range catch missing keys, broken inverse links, unreachable lifecycle states, and similar errors before your code runs.

:::tip[Typed action contracts]
`.Requires(...)`, `.RequiresSoft(...)`, and `.Ensures(...)` translate the
supported expression subset into immutable `ActionPredicate` values. Use their
`ActionPredicate` overloads for CLR-free or explicit Boolean/link/relation
formulas. See [Typed action calculus](/reference/action-calculus/) and the
[3.0 migration guide](/guide/ontology/migration-v3/).
:::

`Object<T>` / `Interface<T>` stay CLR-generic. The first-class CLR-free path is `ObjectTypeFromDescriptor` / `ApplyDelta` — see [Polyglot Descriptors](/guide/ontology/polyglot-descriptors/).

## 2. Register the domain

Call `services.AddOntology(...)` once during DI setup. The extension method instantiates each registered `DomainOntology`, drives its `Define` callback through an internal `OntologyGraphBuilder`, freezes the graph, and registers `IOntologyQuery` along with whichever provider/dispatcher you select.

```csharp
using Strategos.Ontology.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();
    options.UseObjectSetProvider<InMemoryObjectSetProvider>();
    options.UseActionDispatcher<TradingActionDispatcher>();
    options.UseEventStreamProvider<InMemoryEventStreamProvider>();
});
```

`InMemoryObjectSetProvider` is shipped with the core package for tests and local dev. Production deployments swap in `AddPgVectorObjectSets` (from `Strategos.Ontology.Npgsql`) for pgvector-backed storage.

If your provider implements both `IObjectSetProvider` and `IObjectSetWriter`, `AddOntology` registers the same instance against both interfaces automatically.

## 3. Query the graph at runtime

Inject `IOntologyQuery` and ask it questions about the composed graph. The interface lives in `Strategos.Ontology.Query` and is registered as a singleton.

```csharp
using Strategos.Ontology.Actions;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Query;

public sealed class TradingService
{
    private readonly IOntologyQuery _query;

    public TradingService(IOntologyQuery query) => _query = query;

    public IReadOnlyList<ActionDescriptor> ListExecutableActions(Position position)
    {
        var known = new ActionFacts(properties:
        [
            KeyValuePair.Create(
                nameof(Position.Status),
                PredicateLiteral.Enum(position.Status)),
            KeyValuePair.Create(
                nameof(Position.Quantity),
                PredicateLiteral.Integer(position.Quantity)),
        ]);

        return _query.GetValidActions("Position", known);
    }

    public IReadOnlyList<AffectedProperty> Downstream(string property)
        => _query.GetAffectedProperties("Position", property);

    public IReadOnlyList<PostconditionTrace> Effects(string action)
        => _query.TracePostconditions("Position", action);
}
```

`GetCandidateActions` evaluates declared actions against immutable, typed
`ActionFacts` and returns available and indeterminate actions, excluding only
actions proven unavailable. `GetValidActions` is the equivalent descriptor-only
convenience view. Use `GetActionConstraintReport` when you need all three
availability outcomes, per-constraint tri-state detail, and a structured failure reason.
`TracePostconditions` walks `.Modifies(...)`, `.CreatesLinked<T>(...)`, and
`.EmitsEvent<T>(...)` declarations so an agent can plan against the effects of
an action before invoking it.

To materialize the objects themselves, call `_query.GetObjectSet<T>("Position")`. The returned `ObjectSet<T>` is a composable expression tree — `Where`, `TraverseLink`, `SimilarTo`, `Include`, then `ExecuteAsync(ct)`. See [Similarity Search](/strategos/guide/ontology/similarity-search/) for the embedding-aware path.

## Where to go next

- [Similarity Search](/strategos/guide/ontology/similarity-search/) — `ISearchable`, embeddings, and `ExecuteSimilarityAsync`.
- [Text Chunking](/strategos/guide/ontology/text-chunking/) — `ITextChunker` and `SentenceBoundaryChunker` for ingestion.
- [Polyglot Descriptors](/strategos/guide/ontology/polyglot-descriptors/) — registering descriptors that have no CLR type.
- [Platform Architecture §4.14](/strategos/reference/platform-architecture/#414-ontology-layer-strategosontology) for the full design context.
