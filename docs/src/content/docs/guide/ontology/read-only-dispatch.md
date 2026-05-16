---
title: Read-only Dispatch
sidebar:
  order: 7
---

Some ontology actions read state without modifying it — `GetPosition`, `FetchBalance`, lookups against an external system of record. The 2.5.0 dispatch surface separates these from mutating actions so MCP clients can auto-approve the read-only calls and so an integration test can prove a misconfigured action never reaches the write path.

## Marking an action read-only

The DSL gains a `.ReadOnly()` call on `IActionBuilder<T>`:

```csharp
obj.Action("GetPosition")
    .ReadOnly()
    .Returns<Position>();
```

`.ReadOnly()` returns the same builder so the rest of the chain composes normally. The flag flows into the generated `ActionDescriptor.IsReadOnly` initializer.

Two analyzer diagnostics enforce the contract at compile time:

- `AONT036` fires when `.ReadOnly()` is followed by any of `.Modifies(...)`, `.CreatesLinked(...)`, or `.EmitsEvent<T>(...)`. A read-only action that declares a mutating chain call is a contradiction; the analyzer names the offending call so authors can resolve it without re-reading the surrounding builder chain.
- The same chain walk catches the inverse — a non-read-only action that the developer intended as read-only but forgot to mark — once the project enables strict-mode rules.

## Dispatching read-only

`IActionDispatcher` gains a `DispatchReadOnlyAsync` method via a C# default interface implementation:

```csharp
Task<ActionResult> DispatchReadOnlyAsync(
    ActionContext context, object request, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(request);

    if (context.ActionDescriptor?.IsReadOnly is not true)
    {
        return Task.FromResult(new ActionResult(
            IsSuccess: false,
            Error: $"Action '{context.ActionName}' is not read-only."));
    }

    return DispatchAsync(context, request, ct);
}
```

The guard is at the interface, not at any concrete dispatcher, so the invariant cannot be overridden away silently. Existing implementations compile unchanged — the default supplies the read-only path automatically.

`ActionContext` carries an optional `ActionDescriptor` init property. Populate it before calling `DispatchReadOnlyAsync` — typically by looking up the action on `IOntologyQuery.GetActions(objectType)` and selecting by `Name`. The default implementation reads `context.ActionDescriptor?.IsReadOnly` and rejects the call if the descriptor is missing or flags the action as mutating.

## Structured feedback on rejection

A rejected dispatch returns an `ActionResult` whose `Violations` is a `ConstraintViolationReport`:

```csharp
public sealed record ConstraintViolationReport(
    string ActionName,
    IReadOnlyList<ConstraintEvaluation> Hard,
    IReadOnlyList<ConstraintEvaluation> Soft,
    string? SuggestedCorrection);
```

`Hard` holds the unsatisfied preconditions that blocked the call; `Soft` holds advisory ones. `SuggestedCorrection` is populated opportunistically — when the dispatcher's introspection produces a single stable suggestion (for example, a missing precondition property names the property) — and is null otherwise.

The attachment happens in the `ConstraintReportingActionDispatcher` decorator. Opt in via `OntologyOptions.AddConstraintReporting()`:

```csharp
services.AddOntology(options =>
{
    options.AddDomain<TradingDomain>();
    options.UseActionDispatcher<MyDispatcher>();
    options.AddConstraintReporting();
});
```

The decorator sits between any caller-facing decorators and the inner dispatcher (registered at `Order = 25`). It reads `IOntologyQuery.GetActionConstraintReport` after each dispatch and attaches the resulting report to `ActionResult.Violations` when constraints fired. Skip the call and the field stays null; the underlying `ActionResult.Error` still surfaces a short message.

## Instrumenting every dispatch

`IActionDispatchObserver` is the fan-out point for every dispatch — read-only and mutating:

```csharp
public interface IActionDispatchObserver
{
    Task OnDispatchedAsync(
        ActionContext context, ActionResult result, CancellationToken ct);
}
```

Observers are called after the inner dispatcher returns, isolated under try/catch so an observer that throws is logged at warning severity but never fails the dispatch. Register one or more observers in DI, then opt in to the decorator:

```csharp
services.AddSingleton<IActionDispatchObserver, MetricsObserver>();
services.AddSingleton<IActionDispatchObserver, AuditObserver>();
services.AddOntology(options =>
{
    options.AddDomain<TradingDomain>();
    options.UseActionDispatcher<MyDispatcher>();
    options.AddDispatchObservation();
});
```

`AddDispatchObservation` wraps the inner dispatcher with `ObservableActionDispatcher` at `Order = 75`, so when constraint reporting is also enabled, observers see the violation-enriched result. The decorator iterates `IEnumerable<IActionDispatchObserver>` from DI in registration order. The implementation is purely synchronous-shaped — there is no `Task.WhenAll`, just a sequential `await` per observer — so cross-observer ordering is deterministic. OpenTelemetry layering is left to consumers; an observer over `ActivitySource` is a few lines.

The two decorators compose: `AddConstraintReporting()` plus `AddDispatchObservation()` gives every dispatch a violation report and a fan-out to every registered observer, while `DispatchReadOnlyAsync` gates which actions reach the inner dispatcher in the first place.
