---
title: "Core Concepts"
outline: deep
---

# Core Concepts

Understanding these fundamental concepts will help you design effective workflows with Strategos.

## Workflow

A **workflow** is the top-level container for a process. It defines the complete sequence of steps required to accomplish a goal, from start to finish.

```csharp
var workflow = Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Finally<SendConfirmation>();
```

Workflows have:
- A unique identifier (e.g., `"process-order"`)
- A state type that holds data throughout execution
- A sequence of steps that define the process

Think of a workflow as a blueprint. Each time you execute a workflow, you create a new **workflow instance** that progresses through the defined steps.

## State

**State** is the immutable record that captures everything about a workflow's progress at a given moment. It includes:

- Current data (e.g., order details, customer information)
- Execution history (which steps have completed)
- Accumulated results from previous steps

```csharp
public record OrderState
{
    public string OrderId { get; init; }
    public Customer Customer { get; init; }
    public List<LineItem> Items { get; init; }
    public PaymentResult? PaymentResult { get; init; }
    public decimal Total { get; init; }
}
```

State is never modified in place. Instead, each step produces a new state record with updated values. This immutability is key to event sourcing, as it ensures you can always reconstruct any past state by replaying events.

## Step

A **step** is a unit of work within a workflow. Steps can represent:

- **Agent calls** - LLM invocations that analyze data or make decisions
- **Service calls** - External API calls to payment processors, shipping providers, etc.
- **Human reviews** - Approval gates where a person must validate results
- **Computations** - Pure functions that transform state

```csharp
public class ValidateOrder : IStep<OrderState>
{
    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        CancellationToken ct)
    {
        // Validate order and return updated state
        var validatedState = state with { IsValid = true };
        return StepResult.Success(validatedState);
    }
}
```

Steps are the building blocks you compose to create workflows. Each step receives the current state and returns an updated state (or an error).

## Phase

A **phase** represents a discrete position in workflow execution. Phases are generated at compile time from your workflow definition and form a state machine.

```csharp
// Generated at compile time
public enum ProcessOrderPhase
{
    ValidateOrder,
    ProcessPayment,
    SendConfirmation,
    Completed,
    Failed
}
```

Phases enable:
- **Durability** - The workflow can resume from any phase after a crash
- **Visibility** - You always know exactly where a workflow instance stands
- **Routing** - Different logic can be applied based on the current phase

The source generator produces these phase enums automatically, ensuring they stay synchronized with your workflow definition.

## Event

An **event** is an immutable record of something that happened during workflow execution. Events capture:

- Step completions and their results
- Branch decisions and the context that led to them
- Approvals and rejections with reviewer comments
- Errors and compensations

```csharp
// Examples of generated events
public record OrderValidated(string OrderId, bool IsValid, DateTime OccurredAt);
public record PaymentProcessed(string OrderId, decimal Amount, string TransactionId);
public record AgentDecisionMade(string StepId, string Model, double Confidence, string Output);
```

Events are append-only. Once recorded, they're never modified or deleted. This gives you a complete, tamper-proof history of every workflow execution.

## Event Sourcing

**Event sourcing** is the architectural pattern at the heart of Strategos. Instead of storing only the current state, we store the complete sequence of events that produced that state.

### How It Works

1. A workflow step executes and produces results
2. The results are captured as one or more events
3. Events are persisted to the event store (PostgreSQL via Marten)
4. Current state is computed by replaying events through reducers

```csharp
// Reducers compute state from events
public OrderState Apply(OrderState state, PaymentProcessed evt) =>
    state with
    {
        PaymentResult = new PaymentResult(evt.TransactionId, evt.Amount),
        Status = OrderStatus.Paid
    };
```

### Why Event Sourcing Matters for AI

Event sourcing is particularly valuable for AI agent workflows because it captures the full context of every decision:

- **Debugging** - See exactly what input led to an unexpected output
- **Audit compliance** - Prove what model version made each decision
- **Reproducibility** - Replay workflows to understand behavior
- **Analytics** - Analyze patterns across workflow executions

When an agent makes a low-confidence decision or produces an unexpected result, you can trace back through the event history to understand exactly what happened.

## How Concepts Connect

These concepts work together to create reliable AI workflows:

```text
Workflow Definition
       |
       v
    [Step] -----> produces -----> Event
       |                            |
       v                            v
    Phase                     Event Store
    (state machine)           (persistent log)
       |                            |
       v                            v
    State <-------- computed from ----'
```

1. You define a **workflow** with **steps**
2. The generator creates **phases** for each step
3. When steps execute, they produce **events**
4. Events are stored in the **event store**
5. **State** is computed by replaying events through reducers

This architecture gives you the flexibility of agent frameworks with the durability and auditability of enterprise workflow engines.

## What's Next

Now that you understand the core concepts, see how Strategos [compares to alternatives](/learn/comparison) or jump into the [Getting Started guide](/guide/).
