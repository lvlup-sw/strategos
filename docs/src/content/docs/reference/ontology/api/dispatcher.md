---
title: Action Dispatcher
sidebar:
  order: 5
---

`IActionDispatcher` is the runtime entry point for invoking ontology actions. A registered dispatcher receives an `ActionContext` (target identification) and a request payload, looks up the bound implementation (workflow or MCP tool), evaluates preconditions when configured to do so, and returns an `ActionResult` carrying success/error plus optional structured constraint feedback.

Namespace: `Strategos.Ontology.Actions`. Source: `src/Strategos.Ontology/Actions/`.

## IActionDispatcher

| Member | Signature |
|---|---|
| `DispatchAsync` | `Task<ActionResult> DispatchAsync(ActionContext context, object request, CancellationToken ct = default)` |
| `DispatchReadOnlyAsync` (default-impl) | `Task<ActionResult> DispatchReadOnlyAsync(ActionContext context, object request, CancellationToken ct = default)` |

`DispatchAsync` is the unconditional dispatch path — implementations route to the underlying workflow or tool and return its result.

`DispatchReadOnlyAsync` is a default interface method (introduced in 2.5.0, issue #39). It enforces that the supplied `ActionContext.ActionDescriptor` is non-null and its `IsReadOnly` flag is `true`; otherwise it short-circuits with a failure `ActionResult` whose `Error` reads "Action 'X' is not read-only." When the read-only invariant holds, it delegates to `DispatchAsync`. Callers pre-populate `ActionDescriptor` (typically via `IOntologyQuery`) so the dispatch path does not have to re-resolve the descriptor.

Actions declare themselves read-only in the builder DSL:

```csharp
obj.Action("GetQuote")
   .ReadOnly()
   .Accepts<QuoteRequest>()
   .Returns<Quote>()
   .BoundToTool<TradingMcpTools>(t => t.GetQuoteAsync);
```

The `ReadOnly()` setter flips `ActionDescriptor.IsReadOnly` to `true`. Actions without `ReadOnly()` cannot be invoked via `DispatchReadOnlyAsync`.

## ActionContext

`ActionContext` identifies the target of a dispatch. Defined as a `sealed record`:

| Field | Type | Notes |
|---|---|---|
| `Domain` | `string` | Owning domain of the target object type. |
| `ObjectType` | `string` | Simple object type name within the domain. |
| `ObjectId` | `string` | Identifier of the specific instance. |
| `ActionName` | `string` | The action to invoke. |
| `Options` | `ActionDispatchOptions?` | Optional dispatch options (defaults to `null`). |
| `ActionDescriptor` | `ActionDescriptor?` (init) | Optionally pre-resolved descriptor; required for `DispatchReadOnlyAsync`. |

## ActionDispatchOptions

`ActionDispatchOptions` controls dispatch-time enforcement behaviour:

| Field | Type | Default | Notes |
|---|---|---|---|
| `EnforcePreconditions` | `bool` | `false` | When `true`, the dispatcher evaluates action preconditions before dispatch and returns a failure result if any are unsatisfied. |

`ActionDispatchOptions.Default` is a singleton with no enforcement; preconditions and postconditions are metadata by default and enforcement is opt-in.

## ActionResult

`ActionResult` is the dispatch return type, a `sealed record`:

| Field | Type | Notes |
|---|---|---|
| `IsSuccess` | `bool` | True iff the action executed successfully. |
| `Result` | `object?` | Action payload on success; null otherwise. |
| `Error` | `string?` | Failure message; null on success. |
| `Violations` | `ConstraintViolationReport?` | Structured precondition-violation report attached by the constraint-reporting dispatcher decorator when constraints are present. |

`ActionResult` also exposes a three-parameter constructor (`isSuccess`, `result`, `error`) that preserves the pre-2.5.0 shape — existing assemblies linked against the old signature continue to bind. New code should use the primary constructor.

### ConstraintViolationReport

Attached to `ActionResult.Violations` by the constraint-reporting decorator. Schema:

| Field | Type | Notes |
|---|---|---|
| `ActionName` | `string` | The action whose preconditions were evaluated. |
| `Hard` | `IReadOnlyList<ConstraintEvaluation>` | Unsatisfied hard preconditions — these block dispatch. |
| `Soft` | `IReadOnlyList<ConstraintEvaluation>` | Unsatisfied soft preconditions — surfaced as advisory warnings only. |
| `SuggestedCorrection` | `string?` | Optional corrective guidance for the caller. |

## IActionDispatchObserver

`IActionDispatchObserver` is the observation contract — registered observers are notified after every dispatch completes.

| Member | Signature |
|---|---|
| `OnDispatchedAsync` | `Task OnDispatchedAsync(ActionContext context, ActionResult result, CancellationToken ct)` |

The default `ObservableActionDispatcher` decorator isolates each observer and logs (but swallows) exceptions so observation cannot fail a dispatch. Implementations must therefore not throw — observation is best-effort by contract.

Typical observers: structured-logging sinks, audit trails, telemetry exporters, replay harnesses. Multiple observers may be registered; they fire in registration order.

## Decorator composition

Dispatcher decorators (constraint-reporting, observation, custom) are composed via `OntologyOptions.AddDispatcherDecorator(...)` and run in ascending `Order`. The constraint-reporting decorator populates `ActionResult.Violations`; the observation decorator forwards to registered `IActionDispatchObserver` implementations. Custom decorators can short-circuit, transform, or log without modifying the underlying dispatcher.
