# INV-7: Immutable record state; step results never mutate input

`IWorkflowState` implementations are records with `{ get; init; }` properties — no mutable setters. Workflow steps receive a state instance and return a `StepResult<TState>` carrying an *updated* state value (typically `state with { ... }`). Steps must never mutate the input — neither by writing a property, nor by mutating a referenced collection, nor by writing through a held reference.

This is load-bearing for event-sourcing correctness:
- Marten replays events to reconstruct state; a step that mutates input means two replays of the same event log produce different terminal states.
- Time-travel debugging assumes each event maps deterministically to a state delta.
- Saga snapshots could capture mid-mutation state, corrupting subsequent loads.

## Acceptance questions

- Does the proposal introduce an `IWorkflowState`-implementing type with `{ get; set; }` properties?
- Does any step's `ExecuteAsync` write through a reference held by the input state, mutate a referenced collection, or modify a property?
- If state needs to evolve, is the change expressed as `state with { Prop = newValue }` (returning a new record)?
- For collections inside state: are they `ImmutableList<T>` / `ImmutableDictionary<K,V>` (or another truly immutable type), rather than `List<T>` / `Dictionary<K,V>`? `IReadOnlyList<T>` is a read-only view, not an immutable collection — the backing store can still mutate.
- Are non-primitive value types nested inside state also immutable all the way down?

## Repo-grounded checks

- `src/Strategos/Abstractions/IWorkflowState.cs:17-27` — docstring mandates `{ get; init; }` records
- `src/Strategos/Abstractions/IWorkflowStep.cs:44` — explicit "Not mutate the input state - return new state via StepResult"
- `src/Strategos/Steps/StepResult.cs:26-30` — `sealed record StepResult<TState>(TState UpdatedState, ...)`
- `src/Strategos.Generators/StateReducerIncrementalGenerator.cs` + `Emitters/StateReducerEmitter.cs` — SG emits a reducer that applies events to produce new state (event-sourcing compatibility)

## Cross-cutting overlap

**axiom_overlap:** `DIM-7` (primary, Resilience — replay correctness) · `DIM-3` (Contracts — `IWorkflowState` mandates immutability) · `DIM-4` (Test Fidelity — replay tests rely on determinism)

## External grounding

- [Marten projections and replays](https://martendb.io/events/projections/) — projection correctness depends on deterministic event-to-state mapping
- C# `record` + `init`-only properties are the idiomatic immutability primitive for value-like reference types
- The `with` expression is the canonical way to produce an evolved record without mutating the source

## Severity guide

- **HIGH:** A state property uses `set;` rather than `init;`; a step writes through a reference held by the input state; a collection inside state is mutable (`List<T>`, `Dictionary<K,V>`, `HashSet<T>`) and a step calls `.Add()` / `.Remove()` on it.
- **MEDIUM:** State is technically immutable (uses `init;`) but a property's type allows mutation (e.g., `Dictionary<K,V>` rather than `ImmutableDictionary<K,V>`) — defensible only if the codebase enforces immutability at every consumption site.
- **LOW:** A test fixture mutates a `with { }`-produced state. Fine in tests, but flag if it creeps into production code.

## Worked example

**Violation:**

```csharp
public sealed record OrderState : IWorkflowState
{
    public List<string> Items { get; init; } = new();   // List<T> allows mutation
}

public sealed class AddItemStep : IWorkflowStep<OrderState>
{
    public Task<StepResult<OrderState>> ExecuteAsync(OrderState state, CancellationToken ct)
    {
        state.Items.Add("new-item");                    // mutates the input!
        return Task.FromResult(new StepResult<OrderState>(state, ...));
    }
}
```

Replays will not reproduce this deterministically — each replay of the `AddItem` event appends to whatever list the test fixture provided, doubling on the second pass. Saga state diverges from the event log.

**Fix:**

```csharp
public sealed record OrderState : IWorkflowState
{
    public ImmutableList<string> Items { get; init; } = ImmutableList<string>.Empty;
}

public sealed class AddItemStep : IWorkflowStep<OrderState>
{
    public Task<StepResult<OrderState>> ExecuteAsync(OrderState state, CancellationToken ct)
    {
        var next = state with { Items = state.Items.Add("new-item") };
        return Task.FromResult(new StepResult<OrderState>(next, ...));
    }
}
```

`ImmutableList<T>.Add` returns a new list — the original is untouched. Replays are deterministic; time-travel debugging is correct.
