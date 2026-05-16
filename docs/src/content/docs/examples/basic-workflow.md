---
title: "Basic Workflow: Sequential Step Execution"
---

# Basic Workflow: Sequential Step Execution

## The Problem: Business Logic Scattered Across Services

Your e-commerce system processes orders. Payment must complete before shipping. Shipping must complete before confirmation. These aren't arbitrary constraints—they reflect real business dependencies.

**Without workflow orchestration**:
- Tangled callback chains that obscure business logic
- Inconsistent error handling at each integration point
- No visibility into where an order is in its journey
- Manual intervention when processes fail mid-stream
- State scattered across services with no single source of truth

**What you need**: A workflow that:
1. Defines steps as first-class concepts
2. Executes them in a deterministic sequence
3. Survives process restarts without losing progress
4. Makes the current state visible and queryable
5. Handles failures with explicit error paths

This is the **sequential workflow pattern**—the foundation for all other patterns.

---

## Learning Objectives

After this example, you will understand:

- **Workflow definitions** as declarative step sequences
- **Immutable state** and why it matters for reliability
- **Step contracts** via `IWorkflowStep<TState>`
- **State transitions** using the `With()` pattern
- **Error results** for explicit failure handling
- **What gets generated** by the source generator

---

## Conceptual Foundation

### Why Workflows Instead of Code?

Consider processing an order with traditional code:

```csharp
// Traditional approach - problems hidden in plain sight
public async Task ProcessOrder(Order order)
{
    var valid = await _validator.Validate(order);      // What if this fails?
    var payment = await _payments.Charge(order);       // What if payment succeeds but...
    var shipment = await _fulfillment.Ship(order);     // ...shipping fails? Payment is already charged!
    await _notifications.SendConfirmation(order);      // What if notification fails?
}
```

**Problems**:
1. **Atomicity illusion**: Looks atomic, but isn't—partial failures leave inconsistent state
2. **No visibility**: Where is order #12345 right now?
3. **No recovery**: If the process crashes after payment, how do you resume?
4. **No audit**: What happened to this order and when?

**Workflows solve this** by making each step explicit, persisted, and recoverable.

### The Saga Pattern

Sequential workflows implement the **saga pattern**:

```text
Step 1 ──▶ Step 2 ──▶ Step 3 ──▶ Step 4 ──▶ Complete
   │          │          │          │
   ▼          ▼          ▼          ▼
 Event 1   Event 2    Event 3    Event 4
```

Each step:
- Persists its result before continuing
- Emits an event for observability
- Can be retried if the process crashes
- Has a known position in the sequence

### Immutable State: Why Records?

Workflow state is a `record`, not a `class`:

```csharp
[WorkflowState]
public record OrderState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public Order Order { get; init; } = null!;
    public PaymentResult? Payment { get; init; }
    // ...
}
```

**Why immutability?**

| Benefit | Explanation |
|---------|-------------|
| **Replay** | Can rebuild state from events |
| **Debugging** | State at any point is reconstructable |
| **Concurrency** | No shared mutable state to corrupt |
| **Testing** | Predictable, deterministic |

The `With()` pattern creates new state without mutation:

```csharp
// Creates a NEW record with updated Payment
return state
    .With(s => s.Payment, paymentResult)
    .With(s => s.Status, OrderStatus.Paid)
    .AsResult();
```

### Steps as First-Class Concepts

Each step is a class implementing `IWorkflowStep<TState>`:

```csharp
public interface IWorkflowStep<TState> where TState : IWorkflowState
{
    Task<StepResult<TState>> ExecuteAsync(
        TState state,
        StepContext context,
        CancellationToken ct);
}
```

**Why this design?**

| Aspect | Benefit |
|--------|---------|
| **Single responsibility** | Each step does one thing |
| **Dependency injection** | Steps get their dependencies automatically |
| **Testable** | Mock the state, assert the result |
| **Composable** | Combine steps into workflows declaratively |

---

## Design Decisions

| Decision | Why This Approach | Alternative | Trade-off |
|----------|-------------------|-------------|-----------|
| **Records for state** | Immutability for reliability | Classes | Requires `With()` pattern |
| **DI for steps** | Testability, loose coupling | Direct instantiation | More registration code |
| **StepResult return** | Explicit success/failure | Exceptions | More verbose, but clearer |
| **Source generation** | Type-safe, no reflection | Runtime reflection | Compile-time complexity |

### When to Use This Pattern

**Good fit when**:
- Operations must execute in order
- Each step depends on previous results
- You need visibility into workflow progress
- Recovery from failures is important
- Audit trails are required

**Poor fit when**:
- Operations are independent (use Fork/Join)
- Simple request-response with no persistence
- Real-time requirements (saga overhead)

### Anti-Patterns to Avoid

| Anti-Pattern | Problem | Correct Approach |
|--------------|---------|------------------|
| **Mutable state** | Unpredictable behavior | Use immutable records |
| **Side effects in steps** | Can't replay or test | Make steps pure functions of state |
| **Swallowing errors** | Silent failures | Return explicit `StepResult.Fail()` |
| **Giant steps** | Hard to test and debug | Single responsibility per step |
| **Shared state** | Concurrency bugs | All state flows through workflow |

---

## Building the Workflow

### The Shape First

```text
┌───────────────┐    ┌────────────────┐    ┌──────────────┐    ┌──────────────────┐
│ ValidateOrder │───▶│ ProcessPayment │───▶│ FulfillOrder │───▶│ SendConfirmation │
│               │    │                │    │              │    │                  │
│ Check items,  │    │ Charge the     │    │ Ship the     │    │ Notify the       │
│ inventory,    │    │ customer       │    │ items        │    │ customer         │
│ address       │    │                │    │              │    │                  │
└───────────────┘    └────────────────┘    └──────────────┘    └──────────────────┘
       │                    │                    │                      │
       ▼                    ▼                    ▼                      ▼
   Validated             Paid               Shipped              Completed
```

### State: What We Track

```csharp
[WorkflowState]
public record OrderState : IWorkflowState
{
    // Identity - every workflow has a unique ID
    public Guid WorkflowId { get; init; }

    // Input - what was passed to start the workflow
    public Order Order { get; init; } = null!;

    // Step outputs - each step adds its result
    public bool IsValid { get; init; }
    public PaymentResult? Payment { get; init; }
    public ShipmentInfo? Shipment { get; init; }

    // Current status - where are we in the process?
    public OrderStatus Status { get; init; }
}
```

**Why this design?**

- `Order`: The input, never changes after workflow starts
- `IsValid`, `Payment`, `Shipment`: Step outputs, set once per step
- `Status`: Summary of current position for queries

### The Supporting Records

```csharp
public record Order(
    string CustomerId,
    IReadOnlyList<OrderItem> Items,
    Address ShippingAddress);

public record OrderItem(string ProductId, int Quantity, decimal Price);

public record PaymentResult(string TransactionId, bool Success);

public record ShipmentInfo(string TrackingNumber, DateOnly EstimatedDelivery);

public enum OrderStatus { Pending, Validated, Paid, Shipped, Completed }
```

### The Workflow Definition

```csharp
var workflow = Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Then<FulfillOrder>()
    .Finally<SendConfirmation>();
```

**Reading this definition**: "Create a process-order workflow that starts with validating the order, then processes payment, then fulfills the order, and finally sends confirmation."

This reads like a sentence because that's what business logic should be—a clear description of what happens.

### Step Implementation: ValidateOrder

```csharp
public class ValidateOrder : IWorkflowStep<OrderState>
{
    private readonly IOrderValidator _validator;

    // Dependencies injected automatically
    public ValidateOrder(IOrderValidator validator)
    {
        _validator = validator;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        // Do the work
        var result = await _validator.ValidateAsync(state.Order, ct);

        // Explicit failure with error details
        if (!result.IsValid)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("ORDER_INVALID", result.ErrorMessage));
        }

        // Success: return new state (immutable!)
        return state
            .With(s => s.IsValid, true)
            .With(s => s.Status, OrderStatus.Validated)
            .AsResult();
    }
}
```

**Key points**:
- Step receives current state, returns new state
- Failures are explicit, not exceptions
- State is never mutated, only transformed

### Step Implementation: ProcessPayment

```csharp
public class ProcessPayment : IWorkflowStep<OrderState>
{
    private readonly IPaymentService _paymentService;

    public ProcessPayment(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        // Calculate total from order items
        var amount = state.Order.Items.Sum(i => i.Price * i.Quantity);

        // Charge the customer
        var payment = await _paymentService.ChargeAsync(
            state.Order.CustomerId,
            amount,
            ct);

        if (!payment.Success)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("PAYMENT_FAILED", "Payment processing failed"));
        }

        return state
            .With(s => s.Payment, payment)
            .With(s => s.Status, OrderStatus.Paid)
            .AsResult();
    }
}
```

### Step Implementation: FulfillOrder

```csharp
public class FulfillOrder : IWorkflowStep<OrderState>
{
    private readonly IFulfillmentService _fulfillment;

    public FulfillOrder(IFulfillmentService fulfillment)
    {
        _fulfillment = fulfillment;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        var shipment = await _fulfillment.ShipAsync(
            state.Order.Items,
            state.Order.ShippingAddress,
            ct);

        return state
            .With(s => s.Shipment, shipment)
            .With(s => s.Status, OrderStatus.Shipped)
            .AsResult();
    }
}
```

### Step Implementation: SendConfirmation

```csharp
public class SendConfirmation : IWorkflowStep<OrderState>
{
    private readonly INotificationService _notifications;

    public SendConfirmation(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        await _notifications.SendOrderConfirmationAsync(
            state.Order.CustomerId,
            state.Shipment!.TrackingNumber,
            ct);

        return state
            .With(s => s.Status, OrderStatus.Completed)
            .AsResult();
    }
}
```

---

## Registration and Starting

### Service Registration

```csharp
services.AddStrategos()
    .AddWorkflow<ProcessOrderWorkflow>();

// Register step dependencies
services.AddScoped<IOrderValidator, OrderValidator>();
services.AddScoped<IPaymentService, StripePaymentService>();
services.AddScoped<IFulfillmentService, WarehouseFulfillmentService>();
services.AddScoped<INotificationService, EmailNotificationService>();
```

### Starting the Workflow

```csharp
public class OrderController : ControllerBase
{
    private readonly IWorkflowStarter _workflowStarter;

    public OrderController(IWorkflowStarter workflowStarter)
    {
        _workflowStarter = workflowStarter;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
    {
        var workflowId = Guid.NewGuid();
        var initialState = new OrderState
        {
            WorkflowId = workflowId,
            Order = new Order(
                request.CustomerId,
                request.Items,
                request.ShippingAddress)
        };

        await _workflowStarter.StartAsync("process-order", initialState);

        return Accepted(new { WorkflowId = workflowId });
    }
}
```

---

## Generated Artifacts

The source generator produces:

### Phase Enum

```csharp
public enum ProcessOrderPhase
{
    NotStarted,
    ValidateOrder,
    ProcessPayment,
    FulfillOrder,
    SendConfirmation,
    Completed,
    Failed
}
```

### Saga with Handlers

```csharp
public partial class ProcessOrderSaga : Saga<OrderState>
{
    public async Task<object> Handle(
        ExecuteValidateOrderCommand command,
        ValidateOrder step,
        CancellationToken ct)
    {
        // Execute step
        var result = await step.ExecuteAsync(State, context, ct);

        // Apply state update
        State = OrderStateReducer.Reduce(State, result.StateUpdate);

        // Emit event
        // ...

        // Return next command
        return new ExecuteProcessPaymentCommand(WorkflowId);
    }

    // Similar handlers for each step...
}
```

### Commands and Events

```csharp
// Commands
public record StartProcessOrderCommand(Guid WorkflowId, OrderState InitialState);
public record ExecuteValidateOrderCommand(Guid WorkflowId);
public record ExecuteProcessPaymentCommand(Guid WorkflowId);
// ...

// Events
public record ProcessOrderStarted(Guid WorkflowId, DateTimeOffset StartedAt);
public record ProcessOrderPhaseChanged(Guid WorkflowId, ProcessOrderPhase Phase);
public record ProcessOrderCompleted(Guid WorkflowId, DateTimeOffset CompletedAt);
```

---

## The "Aha Moment"

> **A workflow definition is a contract with your future self.**
>
> When this code runs 6 months from now and fails at step 3, you'll know exactly what succeeded, what failed, and where to resume. The workflow state tells you: "Order #12345 is at ProcessPayment, payment failed with error INSUFFICIENT_FUNDS."
>
> That's not debugging—that's operational visibility by design.

---

## Extension Exercises

### Exercise 1: Add Error Handling

When payment fails, refund and notify the customer:

1. Configure error handling for `ProcessPayment`
2. Add `RefundPayment` compensation step
3. Add `SendFailureNotification` step
4. Route failures through compensation path

### Exercise 2: Add Inventory Check

Before shipping, verify inventory is available:

1. Add `InventoryReservation` to state
2. Create `ReserveInventory` step after payment
3. Handle "out of stock" scenario
4. Add compensation to release reservation

### Exercise 3: Add Status Query

Enable querying workflow status:

1. Create `OrderStatusReadModel` projection
2. Subscribe to `ProcessOrderPhaseChanged` events
3. Build query endpoint returning current status
4. Include estimated completion time

---

## Key Takeaways

1. **Workflows make business logic explicit**—steps are named, ordered, visible
2. **Immutable state enables reliability**—replay, debugging, concurrency safety
3. **Steps are testable units**—mock state in, assert state out
4. **Failures are explicit**—`StepResult.Fail()` not exceptions
5. **Source generation provides type safety**—compile-time errors, not runtime surprises
6. **This pattern is the foundation**—branching, loops, forks all build on this

---

## Related

- [Branching Pattern](./branching.md) - Conditional routing based on state
- [Iterative Refinement Pattern](./iterative-refinement.md) - Loops until quality achieved
- [Fork/Join Pattern](./fork-join.md) - Parallel execution with synchronization
