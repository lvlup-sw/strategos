# INV-1: Workflows lower into Wolverine + Marten via Roslyn SG

Strategos's durability story is borrowed, not invented. Workflow runtime is Wolverine messages plus Marten event / document storage, emitted at compile time by `Strategos.Generators`. The DSL produces *definitions*; the source generator lowers those definitions into Wolverine saga classes (with `[SagaIdentity]`, `[Identity]`, `[Version]` markers) and DI extensions that call `services.ConfigureMarten`.

Any hand-rolled durability primitive — a custom saga store, a bespoke event log, a `: Saga`-implementing class authored outside `SagaEmitter` — forks the project away from its main bet and erodes the replay, snapshot, and retry guarantees that ride on top of Wolverine + Marten.

## Acceptance questions

- Does the proposal introduce runtime state that lives outside Marten document storage or the Marten event store?
- Does it add a `: Saga`-implementing class anywhere except `Strategos.Generators/Emitters/SagaEmitter.cs`'s emitted output?
- Does a non-generator project under `src/Strategos*/` take a direct `PackageReference` on `Wolverine.*` or `Marten.*`?
- If a new `PersistenceMode` is added (in addition to `InMemory` / `EventSourced`), does it still target Wolverine + Marten primitives via the existing emitter, or does it spin up a parallel runtime?

## Repo-grounded checks

- `src/Strategos.Generators/Emitters/SagaEmitter.cs:76-85` — emitted usings include `Marten`, `Marten.Schema`, `Wolverine`, `Wolverine.Persistence.Sagas`
- `src/Strategos.Generators/Emitters/SagaEmitter.cs:153-156` — emits `public partial class {sagaClassName} : Saga`
- `src/Strategos.Generators/Emitters/ExtensionsEmitter.cs:45-50, 197-202` — emitted DI extensions wire `services.ConfigureMarten(...)` and `SnapshottedAggregation`
- `src/Strategos.Generators.Tests/SagaEmitterIntegrationTests.cs:40-78` — golden tests assert `: Saga`, `[SagaIdentity]`, `[Identity]`, `[Version]` shape

## Cross-cutting overlap

**axiom_overlap:** `DIM-6` (primary, Architecture — dependency direction) · `DIM-1` (Topology — which components compose at runtime)

This invariant specializes the named axiom dimensions. When `axiom:design` runs in a paired session, INV-1 renders alongside the dimension's generic Design questions.

## External grounding

- [Wolverine sagas](https://wolverinefx.net/guide/durability/sagas.html) — the `Saga` base class, `[SagaIdentity]`, message correlation
- [Marten event sourcing](https://martendb.io/events/) — event stores, projections, snapshots
- `Strategos.Generators` is the only project allowed to know how these two libraries compose

## Severity guide

- **HIGH:** A new file declares `: Saga` outside `SagaEmitter`'s output; a non-generator project adds a Wolverine/Marten `PackageReference`; the DSL grows a path that produces persistence work without going through `SagaEmitter` / `ExtensionsEmitter`.
- **MEDIUM:** The generator emits to Wolverine + Marten correctly but introduces a *parallel* abstraction (e.g., a "lightweight workflow" that uses Marten only without saga wiring) that fragments the runtime story.
- **LOW:** A comment or doc refers to "the Strategos runtime" as if it were independent of Wolverine and Marten.

## Worked example

**Violation:**

```csharp
// src/Strategos.Infrastructure/WorkflowSagaStore.cs — parallel to the generator
public sealed class WorkflowSagaStore : ISagaStore
{
    private readonly Dictionary<Guid, object> _store = new();
    public Task PersistAsync(Guid id, object state) { _store[id] = state; return Task.CompletedTask; }
    // reinvents Marten-shaped storage as a hand-rolled service
}
```

This bypasses Marten entirely — no event log, no replay, no audit trail. The durability promise becomes a lie.

**Fix:**

Add a new `PersistenceMode` enum value, route it through `SagaEmitter` to emit a `: Saga` with the appropriate Marten attributes. The new mode joins `InMemory` / `EventSourced` as a *generator concern*, not a runtime replacement.
