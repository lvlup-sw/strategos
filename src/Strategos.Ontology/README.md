# Strategos.Ontology

Type-safe ontology definition layer for domain modeling. Map your existing C# types into a unified semantic graph of Object Types, Properties, Links, Actions, Events, and Interfaces — validated at compile time, queryable at runtime.

## Installation

```bash
dotnet add package LevelUp.Strategos.Ontology
dotnet add package LevelUp.Strategos.Ontology.Generators
```

## Quick Start

```csharp
using Strategos.Ontology;

public class TradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<Position>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Kind(ObjectKind.Entity);
            obj.Property(p => p.Symbol).Required();
            obj.Property(p => p.UnrealizedPnL).Computed()
               .DerivedFrom(p => p.Quantity, p => p.AverageCost);

            obj.HasMany<TradeOrder>("Orders").Inverse("Position");

            obj.Action("ExecuteTrade")
               .Requires(p => p.Status == PositionStatus.Active)
               .RequiresSoft(p => p.UnrealizedPnL > -10000)
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

## Registration

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingOntology>();
    options.UseObjectSetProvider<MyObjectSetProvider>();
    options.UseActionDispatcher<MyActionDispatcher>();
});
```

## Runtime Queries

Inject `IOntologyQuery` to query the ontology at runtime:

```csharp
// Which actions are valid given current state?
var actions = query.GetValidActions("Position", knownProperties);

// Structured constraint report with failure reasons
var report = query.GetActionConstraintReport("Position", knownProperties);

// What would ExecuteTrade affect?
var effects = query.TracePostconditions("Position", "ExecuteTrade");

// Derivation impact analysis
var affected = query.GetAffectedProperties("Position", "Quantity");
```

## Features

- **Fluent DSL**: Expression-tree-based builder for type-safe ontology definitions
- **Compile-Time Validation**: 35 Roslyn diagnostics (AONT001-AONT035) catch errors at build time
- **Object Types**: Map existing C# types with keys, properties, links, actions, events
- **IS-A Hierarchy**: `IsA<TParent>()` for type subsumption and inheritance queries
- **Preconditions**: Hard (blocking) and soft (advisory) constraints on actions
- **Lifecycle**: State machine declarations with action/event-triggered transitions
- **Derivation Chains**: Transitive property dependency tracking across domains
- **Interfaces**: Polymorphic queries across domain boundaries
- **Extension Points**: Controlled cross-domain link attachment
- **Inverse Links**: Bidirectional traversal with symmetry validation

## Documentation

- **[Platform Architecture — Ontology Layer](https://lvlup-sw.github.io/strategos/reference/platform-architecture#4-strategos-library)**
- **[API Reference](https://lvlup-sw.github.io/strategos/reference/api/ontology)**

## License

MIT
