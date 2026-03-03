# Strategos

Fluent DSL for building durable agentic workflows with Wolverine sagas and Marten event sourcing.

## Installation

```bash
dotnet add package LevelUp.Strategos
dotnet add package LevelUp.Strategos.Generators
```

## Quick Start

```csharp
using Strategos.Builders;
using Strategos.Attributes;

// Define your workflow state
public record OrderState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public OrderStatus Status { get; init; }
    public decimal Total { get; init; }
}

// Define workflow steps
public class ValidateOrder : IWorkflowStep<OrderState> { /* ... */ }
public class ProcessPayment : IWorkflowStep<OrderState> { /* ... */ }
public class FulfillOrder : IWorkflowStep<OrderState> { /* ... */ }

// Build the workflow with fluent DSL
[Workflow("process-order")]
public static partial class ProcessOrderWorkflow
{
    public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
        .Create("process-order")
        .StartWith<ValidateOrder>()
        .Then<ProcessPayment>()
        .Finally<FulfillOrder>();
}
```

## Features

- **Fluent DSL**: Intuitive builder pattern for workflow definition
- **Source Generation**: Wolverine sagas, phase enums, and commands generated at compile time
- **Branching**: Conditional paths with `.Branch()` and `.Case()`
- **Loops**: Iterative refinement with `.RepeatUntil()` and `.While()`
- **Parallelism**: Fork/join patterns with `.Fork()` and `.Join()`
- **Approvals**: Human-in-the-loop with `.AwaitApproval()` and escalation
- **Failure Handling**: Recovery paths with `.OnFailure()`
- **Validation**: Guard clauses with `.Validate()`

## Documentation

- **[Getting Started Guide](https://lvlup-sw.github.io/strategos/guide/)** - Installation and first workflow
- **[Workflow API Reference](https://lvlup-sw.github.io/strategos/reference/api/workflow)** - Complete API documentation
- **[Examples](https://lvlup-sw.github.io/strategos/examples/)** - Real-world workflow patterns

## License

MIT
