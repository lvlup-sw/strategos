---
title: "Your First Workflow"
---

# Your First Workflow

In this tutorial, you will build a complete order processing workflow that validates orders, processes payments, fulfills shipments, and sends confirmations. This example demonstrates the core patterns you will use in every Strategos application.

## What You Will Build

An e-commerce order processing workflow with four sequential steps:

1. **ValidateOrder** - Check order data and inventory
2. **ProcessPayment** - Charge the customer
3. **FulfillOrder** - Ship the items
4. **SendConfirmation** - Notify the customer

Each step receives the current state, performs work, and returns an updated state.

## Step 1: Define the State

Every workflow needs a state object that holds all data as the workflow progresses. State is immutable - steps create new state instances rather than modifying the original.

```csharp
using Strategos;

[WorkflowState]
public record OrderState : IWorkflowState
{
    public Guid WorkflowId { get; init; }
    public Order Order { get; init; } = null!;
    public bool IsValid { get; init; }
    public PaymentResult? Payment { get; init; }
    public ShipmentInfo? Shipment { get; init; }
    public OrderStatus Status { get; init; }
}

public record Order(
    string CustomerId,
    IReadOnlyList<OrderItem> Items,
    Address ShippingAddress);

public record OrderItem(string ProductId, int Quantity, decimal Price);

public record PaymentResult(string TransactionId, bool Success);

public record ShipmentInfo(string TrackingNumber, DateOnly EstimatedDelivery);

public enum OrderStatus { Pending, Validated, Paid, Shipped, Completed }
```

The `[WorkflowState]` attribute triggers source generation, creating reducer logic and serialization support automatically.

## Step 2: Define the Workflow

The workflow definition describes the sequence of steps. This is the "what" of your workflow - the actual implementation comes in the steps.

```csharp
var workflow = Workflow<OrderState>
    .Create("process-order")
    .StartWith<ValidateOrder>()
    .Then<ProcessPayment>()
    .Then<FulfillOrder>()
    .Finally<SendConfirmation>();
```

This reads naturally: "Create a process-order workflow. Start with validating the order, then process payment, then fulfill the order, and finally send confirmation."

## Step 3: Implement the Steps

Each step implements `IWorkflowStep<TState>` and contains the business logic.

### ValidateOrder

```csharp
public class ValidateOrder : IWorkflowStep<OrderState>
{
    private readonly IOrderValidator _validator;

    public ValidateOrder(IOrderValidator validator)
    {
        _validator = validator;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(state.Order, ct);

        if (!validationResult.IsValid)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("ORDER_INVALID", validationResult.ErrorMessage));
        }

        return state
            .With(s => s.IsValid, true)
            .With(s => s.Status, OrderStatus.Validated)
            .AsResult();
    }
}
```

Notice several patterns:

- **Dependency injection** - Services are injected via constructor
- **Immutable state updates** - Use `With()` to create new state with updated values
- **Explicit failures** - Return `StepResult.Fail()` with error details
- **Cancellation support** - Always pass the `CancellationToken` to async operations

### ProcessPayment

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
        var amount = state.Order.Items.Sum(i => i.Price * i.Quantity);
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

### FulfillOrder

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

### SendConfirmation

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

## Step 4: Register Services

Register the workflow and its dependencies in your DI container:

```csharp
services.AddStrategos()
    .AddWorkflow<ProcessOrderWorkflow>();

// Register step dependencies
services.AddScoped<IOrderValidator, OrderValidator>();
services.AddScoped<IPaymentService, StripePaymentService>();
services.AddScoped<IFulfillmentService, WarehouseFulfillmentService>();
services.AddScoped<INotificationService, EmailNotificationService>();
```

## Step 5: Start the Workflow

Trigger the workflow from an API controller or service:

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

The workflow executes asynchronously. Return immediately with the workflow ID so clients can track progress.

## Understanding Generated Artifacts

The source generator creates several artifacts from your workflow definition:

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

Use this enum to track workflow progress and query workflows by phase.

### Wolverine Saga

```csharp
public class ProcessOrderSaga : Saga
{
    public OrderState State { get; set; }

    public async Task Handle(ExecuteValidateOrderCommand command, ...)
    {
        // Generated handler code
    }
}
```

The saga manages durability - if your process restarts, workflows resume from the last completed step.

### Commands and Events

```csharp
// Commands
public record StartProcessOrderCommand(Guid WorkflowId, OrderState InitialState);
public record ExecuteValidateOrderCommand(Guid WorkflowId);
public record ExecuteProcessPaymentCommand(Guid WorkflowId);

// Events
public record ProcessOrderStarted(Guid WorkflowId, DateTimeOffset StartedAt);
public record ProcessOrderPhaseChanged(Guid WorkflowId, ProcessOrderPhase Phase);
public record ProcessOrderCompleted(Guid WorkflowId, OrderState FinalState);
```

These enable event sourcing, audit trails, and integration with other systems.

## Key Points

- **State is immutable** - Use `With()` to create updated copies
- **Steps are DI-friendly** - Inject services via constructor
- **Explicit error handling** - Return `StepResult.Fail()` for business failures
- **Durable by default** - Workflows survive process restarts via Wolverine
- **Generated code** - Phase enums, sagas, commands, and events are created automatically

## Next Steps

You have built a linear workflow where steps execute in sequence. Real workflows often need more complex control flow:

- [Branching](./branching) - Route to different paths based on conditions
- [Parallel Execution](./parallel) - Run independent steps concurrently
- [Loops](./loops) - Iterate until a condition is met
- [Approvals](./approvals) - Pause for human input
