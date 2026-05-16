---
title: "Source Generators"
---

# Source Generators

The `Strategos.Generators` package transforms fluent DSL definitions into type-safe artifacts at compile time.

## Generated Artifacts Overview

| Artifact | Purpose |
|----------|---------|
| Phase Enum | Type-safe enumeration of workflow phases |
| Commands | Wolverine message types for step transitions |
| Events | Marten event types for audit trail |
| Saga Class | Complete Wolverine saga with handlers |
| State Reducers | Property merge logic based on attributes |
| DI Extensions | Service registration helpers |

---

## Phase Enum

Generated enumeration representing all workflow phases.

### Generation Pattern

For a workflow named `process-order` with steps `ValidateOrder`, `ProcessPayment`, `FulfillOrder`:

```csharp
// Generated
public enum ProcessOrderPhase
{
    NotStarted,
    ValidateOrder,
    ProcessPayment,
    FulfillOrder,
    Completed,
    Failed
}
```

### Special Values

| Value | Description |
|-------|-------------|
| `NotStarted` | Initial state before workflow begins |
| `Completed` | Terminal state after successful completion |
| `Failed` | Terminal state after failure |

---

## Commands

Generated Wolverine command types for triggering step execution.

### Generation Pattern

```csharp
// Generated for each step
public record ExecuteValidateOrderCommand(
    [property: SagaIdentity] Guid WorkflowId);

public record ExecuteProcessPaymentCommand(
    [property: SagaIdentity] Guid WorkflowId);

public record ExecuteFulfillOrderCommand(
    [property: SagaIdentity] Guid WorkflowId);
```

### Attributes

| Attribute | Purpose |
|-----------|---------|
| `[SagaIdentity]` | Links command to saga instance |

---

## Events

Generated Marten event types for audit trail.

### Generation Pattern

```csharp
// Workflow lifecycle events
public record ProcessOrderWorkflowStarted(Guid WorkflowId, DateTimeOffset Timestamp);
public record ProcessOrderWorkflowCompleted(Guid WorkflowId, DateTimeOffset Timestamp);

// Phase transition events
public record ProcessOrderPhaseChanged(
    Guid WorkflowId,
    ProcessOrderPhase FromPhase,
    ProcessOrderPhase ToPhase,
    DateTimeOffset Timestamp);

// Step completion events
public record ValidateOrderStepCompleted(
    Guid WorkflowId,
    DateTimeOffset Timestamp,
    OrderState StateSnapshot);
```

### Event Properties

| Property | Type | Description |
|----------|------|-------------|
| `WorkflowId` | `Guid` | Workflow instance identifier |
| `Timestamp` | `DateTimeOffset` | When the event occurred |
| `FromPhase` | `Enum` | Previous phase (for transitions) |
| `ToPhase` | `Enum` | New phase (for transitions) |
| `StateSnapshot` | `TState` | State at completion (for step events) |

---

## Saga Class

Generated Wolverine saga containing all handlers.

### Generation Pattern

```csharp
// Generated saga
public partial class ProcessOrderSaga : Saga
{
    public Guid WorkflowId { get; set; }
    public OrderState State { get; set; }
    public ProcessOrderPhase Phase { get; set; }

    // Entry point
    public async Task<ExecuteValidateOrderCommand> Start(
        StartProcessOrderCommand command,
        CancellationToken ct)
    {
        WorkflowId = command.WorkflowId;
        State = command.InitialState;
        Phase = ProcessOrderPhase.ValidateOrder;
        return new ExecuteValidateOrderCommand(WorkflowId);
    }

    // Step handlers
    public async Task<ExecuteProcessPaymentCommand> Handle(
        ExecuteValidateOrderCommand command,
        ValidateOrderStep step,
        CancellationToken ct)
    {
        var result = await step.ExecuteAsync(State, CreateContext(), ct);
        State = ProcessOrderStateReducer.Reduce(State, result.State);
        Phase = ProcessOrderPhase.ProcessPayment;
        return new ExecuteProcessPaymentCommand(WorkflowId);
    }

    // Additional handlers...
}
```

### Saga Properties

| Property | Type | Description |
|----------|------|-------------|
| `WorkflowId` | `Guid` | Saga identity |
| `State` | `TState` | Current workflow state |
| `Phase` | `Enum` | Current workflow phase |

---

## State Reducers

Generated logic for merging state updates based on attributes.

### Generation Pattern

```csharp
// For state with [Append] and [Merge] attributes
public static class ProcessOrderStateReducer
{
    public static OrderState Reduce(OrderState current, OrderState update)
    {
        return current with
        {
            // Regular properties: take update value
            Status = update.Status ?? current.Status,
            TotalAmount = update.TotalAmount,

            // [Append] properties: combine lists
            AuditLog = current.AuditLog
                .Concat(update.AuditLog ?? Enumerable.Empty<string>())
                .ToList(),

            // [Merge] properties: combine dictionaries
            Metadata = current.Metadata
                .Concat(update.Metadata ?? new())
                .GroupBy(kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.Last().Value)
        };
    }
}
```

### Merge Behaviors

| Attribute | Behavior |
|-----------|----------|
| (none) | Update replaces current |
| `[Append]` | Lists are concatenated |
| `[Merge]` | Dictionaries are combined, newer wins |

---

## DI Extensions

Generated service registration helpers.

### Generation Pattern

```csharp
// Generated extension method
public static class ProcessOrderWorkflowExtensions
{
    public static IServiceCollection AddProcessOrderWorkflow(
        this IServiceCollection services)
    {
        // Register step implementations
        services.AddScoped<ValidateOrderStep>();
        services.AddScoped<ProcessPaymentStep>();
        services.AddScoped<FulfillOrderStep>();

        // Register saga
        services.AddScoped<ProcessOrderSaga>();

        return services;
    }
}
```

### Usage

```csharp
services.AddStrategos()
    .AddProcessOrderWorkflow();

// Or add all workflows in assembly
services.AddStrategos()
    .AddWorkflowsFromAssembly(typeof(ProcessOrderWorkflow).Assembly);
```

---

## Triggering Generation

Generation is triggered by the `[Workflow]` attribute on a class containing a workflow definition.

### Required Pattern

```csharp
using Strategos;

namespace MyApp.Workflows;

[Workflow("process-order")]
public static partial class ProcessOrderWorkflow
{
    public static WorkflowDefinition<OrderState> Definition =>
        Workflow<OrderState>.Create("process-order")
            .StartWith<ValidateOrderStep>()
            .Then<ProcessPaymentStep>()
            .Finally<FulfillOrderStep>();
}
```

### Requirements

| Requirement | Description |
|-------------|-------------|
| `[Workflow]` attribute | Triggers source generation |
| `partial` modifier | Allows generator to extend class |
| `static` modifier | Workflow definitions are static |
| Namespace | Cannot be in global namespace |

---

## Generated File Location

Generated files appear in the `obj` directory and are visible in IDE under Dependencies > Analyzers:

```text
obj/
  Debug/
    net8.0/
      generated/
        Strategos.Generators/
          ProcessOrderWorkflow.g.cs
          ProcessOrderSaga.g.cs
          ProcessOrderStateReducer.g.cs
```

::: tip Viewing Generated Code
In Visual Studio, expand Dependencies > Analyzers > Strategos.Generators to see generated files.
In Rider, use Navigate > Generated Code.
:::
