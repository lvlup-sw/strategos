---
title: "Workflow API"
---

# Workflow API

Core types for defining and executing workflows in `Strategos`.

## Workflow\<TState\>

Entry point for fluent workflow definitions. Creates a workflow builder for the specified state type.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create(string name)` | `IWorkflowBuilder<TState>` | Creates a new workflow with the given name |

### Example

```csharp
Workflow<OrderState>.Create("process-order")
    .StartWith<ValidateOrderStep>()
    .Then<ProcessPaymentStep>()
    .Finally<FulfillOrderStep>();
```

---

## IWorkflowStep\<TState\>

Interface for implementing workflow steps. Each step receives state, executes logic, and returns updated state.

### Methods

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `ExecuteAsync` | `TState state`, `StepContext context`, `CancellationToken ct` | `Task<StepResult<TState>>` | Executes the step logic |

### Example

```csharp
public class ValidateOrderStep : IWorkflowStep<OrderState>
{
    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        var isValid = await ValidateAsync(state.Order, ct);

        return state
            .With(s => s.IsValid, isValid)
            .AsResult();
    }
}
```

---

## IWorkflowDefinition\<TState\>

Interface for workflow definition classes. Implemented by generated partial classes.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Definition` | `WorkflowDefinition<TState>` | The complete workflow definition |

### Example

```csharp
[Workflow("process-order")]
public static partial class ProcessOrderWorkflow : IWorkflowDefinition<OrderState>
{
    public static WorkflowDefinition<OrderState> Definition =>
        Workflow<OrderState>.Create("process-order")
            .StartWith<ValidateOrderStep>()
            .Finally<CompleteOrderStep>();
}
```

---

## StepResult\<TState\>

Result type returned from step execution. Contains the updated state and optional routing information.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `State` | `TState` | The updated workflow state |
| `BranchValue` | `object?` | Optional value for branch routing |
| `IsComplete` | `bool` | Whether workflow should terminate |

### Creation Methods

| Method | Description |
|--------|-------------|
| `state.AsResult()` | Creates result with updated state |
| `state.AsResult(branchValue)` | Creates result with branch routing |
| `StepResult<TState>.Complete(state)` | Creates terminal result |

### Example

```csharp
// Simple state update
return state.With(s => s.Status, "Validated").AsResult();

// With branch routing
return state.AsResult(state.OrderType);  // Routes based on OrderType

// Terminal result
return StepResult<OrderState>.Complete(state);
```

---

## StepContext

Execution context passed to every step. Contains metadata about the current execution.

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `WorkflowId` | `Guid` | Unique identifier for this workflow instance |
| `CorrelationId` | `string` | Correlation ID for tracing |
| `Timestamp` | `DateTimeOffset` | When the step execution started |
| `Phase` | `string` | Current workflow phase name |
| `StepName` | `string` | Current step name |
| `Metadata` | `IReadOnlyDictionary<string, object>` | Additional context data |

### Example

```csharp
public async Task<StepResult<OrderState>> ExecuteAsync(
    OrderState state,
    StepContext context,
    CancellationToken ct)
{
    _logger.LogInformation(
        "Processing order {WorkflowId} at phase {Phase}",
        context.WorkflowId,
        context.Phase);

    // Step logic...
}
```

---

## State Attributes

Attributes that control how state properties are merged between steps.

### [WorkflowState]

Marks a record as workflow state. Required for source generator to produce state reducers.

```csharp
[WorkflowState]
public record OrderState
{
    public Guid OrderId { get; init; }
    public string Status { get; init; }
}
```

### [Append]

Merge lists by appending new items to existing items.

| Constraint | Value |
|------------|-------|
| Valid On | Collection properties (`List<T>`, `IList<T>`, etc.) |
| Behavior | Combines source and target lists |

```csharp
[WorkflowState]
public record OrderState
{
    [Append]
    public List<string> AuditLog { get; init; } = new();
}
```

**Merge Behavior:**
```csharp
// Before: AuditLog = ["Created", "Validated"]
// Update: AuditLog = ["Payment processed"]
// After:  AuditLog = ["Created", "Validated", "Payment processed"]
```

### [Merge]

Merge dictionaries. New values overwrite existing keys.

| Constraint | Value |
|------------|-------|
| Valid On | Dictionary properties (`Dictionary<TKey, TValue>`) |
| Behavior | Combines dictionaries, newer values win |

```csharp
[WorkflowState]
public record OrderState
{
    [Merge]
    public Dictionary<string, decimal> LinePrices { get; init; } = new();
}
```

**Merge Behavior:**
```csharp
// Before: LinePrices = {"item1": 10.00, "item2": 20.00}
// Update: LinePrices = {"item2": 25.00, "item3": 30.00}
// After:  LinePrices = {"item1": 10.00, "item2": 25.00, "item3": 30.00}
```

---

## Fluent Builder Methods

Methods available on the workflow builder for constructing workflow definitions.

### Flow Control

| Method | Description |
|--------|-------------|
| `StartWith<TStep>()` | First step in workflow (required) |
| `Then<TStep>()` | Sequential step |
| `Finally<TStep>()` | Terminal step (recommended) |

### Branching

| Method | Description |
|--------|-------------|
| `Branch(selector, cases...)` | Route based on state value |
| `BranchCase<TValue>(value, builder)` | Define branch case |
| `Otherwise(builder)` | Default branch case |

### Parallel Execution

| Method | Description |
|--------|-------------|
| `Fork(paths...)` | Execute paths in parallel |
| `Join<TStep>()` | Merge parallel results |

### Loops

| Method | Description |
|--------|-------------|
| `RepeatUntil(condition, name, builder)` | Loop until condition is true |

### Human-in-the-Loop

| Method | Description |
|--------|-------------|
| `AwaitApproval<TStep>()` | Pause for human approval |

### Example

```csharp
Workflow<OrderState>.Create("process-order")
    .StartWith<ValidateOrderStep>()
    .Branch(s => s.OrderType,
        BranchCase<OrderType>(OrderType.Standard, path => path
            .Then<ProcessStandardStep>()),
        BranchCase<OrderType>(OrderType.Express, path => path
            .Then<ProcessExpressStep>()),
        Otherwise(path => path
            .Then<ProcessCustomStep>()))
    .Fork(
        path => path.Then<NotifyCustomerStep>(),
        path => path.Then<UpdateInventoryStep>())
    .Join<AggregateResultsStep>()
    .AwaitApproval<ShipmentApprovalStep>()
    .Finally<FulfillOrderStep>();
```
