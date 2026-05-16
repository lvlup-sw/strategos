---
title: "Order Processing Workflow"
---

# Order Processing Workflow

A complete e-commerce order processing workflow demonstrating linear execution, error handling, and compensation patterns.

## Overview

This example implements a typical e-commerce order fulfillment process. When a customer places an order, the workflow validates the order, processes payment, reserves inventory, ships the product, and sends confirmation. If any step fails, the workflow executes compensation logic to roll back previous actions.

**Use this pattern when:**
- Processing requires sequential steps with clear dependencies
- Failures need to trigger rollback of previous actions
- Each step has external side effects that may need reversal
- Auditability of the complete process is required

## State Definition

```csharp
[WorkflowState]
public record OrderState : IWorkflowState
{
    public Guid WorkflowId { get; init; }

    // Order details
    public Order Order { get; init; } = null!;
    public OrderStatus Status { get; init; } = OrderStatus.Pending;

    // Step results
    public ValidationResult? Validation { get; init; }
    public PaymentResult? Payment { get; init; }
    public InventoryReservation? Reservation { get; init; }
    public ShipmentInfo? Shipment { get; init; }
    public bool NotificationSent { get; init; }

    // Error tracking
    public OrderError? Error { get; init; }

    // Audit trail
    [Append]
    public ImmutableList<OrderEvent> EventHistory { get; init; } = [];
}

public record Order(
    Guid OrderId,
    string CustomerId,
    IReadOnlyList<OrderItem> Items,
    Address ShippingAddress,
    Address? BillingAddress,
    PaymentMethod PaymentMethod,
    decimal TotalAmount);

public record OrderItem(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record Address(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country);

public record PaymentMethod(
    PaymentType Type,
    string TokenizedId);  // Tokenized payment info, never store raw card data

public record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);

public record PaymentResult(
    string TransactionId,
    PaymentStatus Status,
    decimal AmountCharged,
    DateTimeOffset ProcessedAt);

public record InventoryReservation(
    string ReservationId,
    IReadOnlyList<ReservedItem> Items,
    DateTimeOffset ExpiresAt);

public record ReservedItem(
    string ProductId,
    int QuantityReserved,
    string WarehouseId);

public record ShipmentInfo(
    string TrackingNumber,
    string Carrier,
    DateOnly EstimatedDelivery,
    DateTimeOffset ShippedAt);

public record OrderError(
    string Code,
    string Message,
    string? StepName);

public record OrderEvent(
    string EventType,
    DateTimeOffset Timestamp,
    string Details);

public enum OrderStatus
{
    Pending,
    Validated,
    PaymentProcessed,
    InventoryReserved,
    Shipped,
    Completed,
    Failed,
    Compensated
}

public enum PaymentType { CreditCard, DebitCard, PayPal, BankTransfer }

public enum PaymentStatus { Approved, Declined, Pending, Refunded }
```

## Workflow Definition

```csharp
public class ProcessOrderWorkflow
{
    public static Workflow<OrderState> Create() =>
        Workflow<OrderState>
            .Create("process-order")
            .StartWith<ValidateOrder>()
            .Then<ProcessPayment>()
            .Then<ReserveInventory>()
            .Then<CreateShipment>()
            .Finally<SendConfirmation>();
}
```

This linear workflow processes orders step by step. Each step depends on the previous step completing successfully.

## Step Implementations

### ValidateOrder

```csharp
public class ValidateOrder : IWorkflowStep<OrderState>
{
    private readonly IOrderValidator _validator;
    private readonly IProductCatalog _catalog;
    private readonly TimeProvider _time;

    public ValidateOrder(
        IOrderValidator validator,
        IProductCatalog catalog,
        TimeProvider time)
    {
        _validator = validator;
        _catalog = catalog;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        var errors = new List<string>();

        // Validate order structure
        if (!state.Order.Items.Any())
        {
            errors.Add("Order must contain at least one item");
        }

        // Validate each item exists and has valid quantity
        foreach (var item in state.Order.Items)
        {
            var product = await _catalog.GetProductAsync(item.ProductId, ct);
            if (product is null)
            {
                errors.Add($"Product {item.ProductId} not found");
                continue;
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"Invalid quantity for {item.ProductName}");
            }

            if (item.UnitPrice != product.CurrentPrice)
            {
                errors.Add($"Price mismatch for {item.ProductName}");
            }
        }

        // Validate shipping address
        var addressValidation = await _validator.ValidateAddressAsync(
            state.Order.ShippingAddress, ct);
        if (!addressValidation.IsValid)
        {
            errors.AddRange(addressValidation.Errors);
        }

        // Validate total calculation
        var expectedTotal = state.Order.Items.Sum(i => i.LineTotal);
        if (Math.Abs(expectedTotal - state.Order.TotalAmount) > 0.01m)
        {
            errors.Add("Order total calculation mismatch");
        }

        var result = new ValidationResult(errors.Count == 0, errors);

        if (!result.IsValid)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("ORDER_VALIDATION_FAILED",
                    string.Join("; ", result.Errors)));
        }

        var orderEvent = new OrderEvent(
            "OrderValidated",
            _time.GetUtcNow(),
            $"Order validated with {state.Order.Items.Count} items");

        return state
            .With(s => s.Validation, result)
            .With(s => s.Status, OrderStatus.Validated)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}
```

### ProcessPayment

```csharp
public class ProcessPayment : IWorkflowStep<OrderState>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _time;

    public ProcessPayment(IPaymentGateway paymentGateway, TimeProvider time)
    {
        _paymentGateway = paymentGateway;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        // Idempotency check - if already processed, skip
        if (state.Payment is not null && state.Payment.Status == PaymentStatus.Approved)
        {
            return state.AsResult();
        }

        var paymentRequest = new PaymentRequest
        {
            OrderId = state.Order.OrderId,
            CustomerId = state.Order.CustomerId,
            Amount = state.Order.TotalAmount,
            Currency = "USD",
            PaymentMethodToken = state.Order.PaymentMethod.TokenizedId,
            IdempotencyKey = $"order-{state.Order.OrderId}"
        };

        PaymentResult result;
        try
        {
            result = await _paymentGateway.ChargeAsync(paymentRequest, ct);
        }
        catch (PaymentGatewayException ex)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("PAYMENT_GATEWAY_ERROR", ex.Message));
        }

        if (result.Status == PaymentStatus.Declined)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("PAYMENT_DECLINED",
                    "Payment was declined by the payment processor"));
        }

        var orderEvent = new OrderEvent(
            "PaymentProcessed",
            _time.GetUtcNow(),
            $"Payment of ${result.AmountCharged} processed, transaction: {result.TransactionId}");

        return state
            .With(s => s.Payment, result)
            .With(s => s.Status, OrderStatus.PaymentProcessed)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}
```

### ReserveInventory

```csharp
public class ReserveInventory : IWorkflowStep<OrderState>
{
    private readonly IInventoryService _inventory;
    private readonly TimeProvider _time;

    public ReserveInventory(IInventoryService inventory, TimeProvider time)
    {
        _inventory = inventory;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        // Idempotency check
        if (state.Reservation is not null)
        {
            return state.AsResult();
        }

        var reservationRequest = new ReservationRequest
        {
            OrderId = state.Order.OrderId,
            Items = state.Order.Items.Select(i => new ReservationItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList(),
            ShippingAddress = state.Order.ShippingAddress
        };

        InventoryReservation reservation;
        try
        {
            reservation = await _inventory.ReserveAsync(reservationRequest, ct);
        }
        catch (InsufficientInventoryException ex)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("INSUFFICIENT_INVENTORY",
                    $"Not enough stock for: {string.Join(", ", ex.ProductIds)}"));
        }

        var orderEvent = new OrderEvent(
            "InventoryReserved",
            _time.GetUtcNow(),
            $"Reserved {reservation.Items.Count} items from {reservation.Items.Select(i => i.WarehouseId).Distinct().Count()} warehouses");

        return state
            .With(s => s.Reservation, reservation)
            .With(s => s.Status, OrderStatus.InventoryReserved)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}
```

### CreateShipment

```csharp
public class CreateShipment : IWorkflowStep<OrderState>
{
    private readonly IShippingService _shipping;
    private readonly TimeProvider _time;

    public CreateShipment(IShippingService shipping, TimeProvider time)
    {
        _shipping = shipping;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        // Idempotency check
        if (state.Shipment is not null)
        {
            return state.AsResult();
        }

        var shipmentRequest = new ShipmentRequest
        {
            OrderId = state.Order.OrderId,
            Destination = state.Order.ShippingAddress,
            Items = state.Reservation!.Items.Select(r => new ShipmentItem
            {
                ProductId = r.ProductId,
                Quantity = r.QuantityReserved,
                SourceWarehouse = r.WarehouseId
            }).ToList()
        };

        ShipmentInfo shipment;
        try
        {
            shipment = await _shipping.CreateShipmentAsync(shipmentRequest, ct);
        }
        catch (ShippingException ex)
        {
            return StepResult.Fail<OrderState>(
                Error.Create("SHIPPING_ERROR", ex.Message));
        }

        var orderEvent = new OrderEvent(
            "ShipmentCreated",
            _time.GetUtcNow(),
            $"Shipment created with {shipment.Carrier}, tracking: {shipment.TrackingNumber}");

        return state
            .With(s => s.Shipment, shipment)
            .With(s => s.Status, OrderStatus.Shipped)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}
```

### SendConfirmation

```csharp
public class SendConfirmation : IWorkflowStep<OrderState>
{
    private readonly INotificationService _notifications;
    private readonly ICustomerService _customers;
    private readonly TimeProvider _time;

    public SendConfirmation(
        INotificationService notifications,
        ICustomerService customers,
        TimeProvider time)
    {
        _notifications = notifications;
        _customers = customers;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        // Idempotency check
        if (state.NotificationSent)
        {
            return state.AsResult();
        }

        var customer = await _customers.GetCustomerAsync(state.Order.CustomerId, ct);

        var notification = new OrderConfirmationNotification
        {
            RecipientEmail = customer.Email,
            RecipientName = customer.Name,
            OrderId = state.Order.OrderId,
            OrderTotal = state.Order.TotalAmount,
            Items = state.Order.Items,
            TrackingNumber = state.Shipment!.TrackingNumber,
            Carrier = state.Shipment.Carrier,
            EstimatedDelivery = state.Shipment.EstimatedDelivery
        };

        await _notifications.SendOrderConfirmationAsync(notification, ct);

        var orderEvent = new OrderEvent(
            "ConfirmationSent",
            _time.GetUtcNow(),
            $"Order confirmation sent to {customer.Email}");

        return state
            .With(s => s.NotificationSent, true)
            .With(s => s.Status, OrderStatus.Completed)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}
```

## Error Handling with Compensation

When a step fails after previous steps have completed, we need to roll back those actions. Compensation handlers reverse the effects of previous steps.

### Compensation Step Implementations

```csharp
public class RefundPayment : IWorkflowStep<OrderState>
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly TimeProvider _time;

    public RefundPayment(IPaymentGateway paymentGateway, TimeProvider time)
    {
        _paymentGateway = paymentGateway;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        if (state.Payment is null || state.Payment.Status == PaymentStatus.Refunded)
        {
            return state.AsResult();
        }

        await _paymentGateway.RefundAsync(new RefundRequest
        {
            TransactionId = state.Payment.TransactionId,
            Amount = state.Payment.AmountCharged,
            Reason = state.Error?.Message ?? "Order cancelled"
        }, ct);

        var refundedPayment = state.Payment with { Status = PaymentStatus.Refunded };

        var orderEvent = new OrderEvent(
            "PaymentRefunded",
            _time.GetUtcNow(),
            $"Refunded ${state.Payment.AmountCharged} for transaction {state.Payment.TransactionId}");

        return state
            .With(s => s.Payment, refundedPayment)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}

public class ReleaseInventory : IWorkflowStep<OrderState>
{
    private readonly IInventoryService _inventory;
    private readonly TimeProvider _time;

    public ReleaseInventory(IInventoryService inventory, TimeProvider time)
    {
        _inventory = inventory;
        _time = time;
    }

    public async Task<StepResult<OrderState>> ExecuteAsync(
        OrderState state,
        StepContext context,
        CancellationToken ct)
    {
        if (state.Reservation is null)
        {
            return state.AsResult();
        }

        await _inventory.ReleaseReservationAsync(state.Reservation.ReservationId, ct);

        var orderEvent = new OrderEvent(
            "InventoryReleased",
            _time.GetUtcNow(),
            $"Released reservation {state.Reservation.ReservationId}");

        return state
            .With(s => s.Reservation, null)
            .With(s => s.EventHistory, state.EventHistory.Add(orderEvent))
            .AsResult();
    }
}
```

### Compensation Workflow

Define a separate compensation workflow that runs when the main workflow fails:

```csharp
public class CompensateOrderWorkflow
{
    public static Workflow<OrderState> Create() =>
        Workflow<OrderState>
            .Create("compensate-order")
            .StartWith<ReleaseInventory>()
            .Then<RefundPayment>()
            .Finally<SendCancellationNotice>();
}
```

### Triggering Compensation

In your Wolverine message handler, catch failures and trigger compensation:

```csharp
public static class OrderFailureHandler
{
    public static async Task Handle(
        OrderWorkflowFailed failure,
        IMessageBus bus,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Load the failed workflow state
        var saga = await session.LoadAsync<ProcessOrderSaga>(failure.WorkflowId, ct);

        if (saga is null) return;

        // Record the error
        var stateWithError = saga.State
            .With(s => s.Status, OrderStatus.Failed)
            .With(s => s.Error, new OrderError(
                failure.ErrorCode,
                failure.ErrorMessage,
                failure.FailedStep));

        // Start compensation workflow
        await bus.InvokeAsync(new StartCompensateOrderCommand(
            failure.WorkflowId,
            stateWithError), ct);
    }
}
```

## Service Interfaces

```csharp
public interface IOrderValidator
{
    Task<AddressValidationResult> ValidateAddressAsync(Address address, CancellationToken ct);
}

public interface IProductCatalog
{
    Task<Product?> GetProductAsync(string productId, CancellationToken ct);
}

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct);
    Task RefundAsync(RefundRequest request, CancellationToken ct);
}

public interface IInventoryService
{
    Task<InventoryReservation> ReserveAsync(ReservationRequest request, CancellationToken ct);
    Task ReleaseReservationAsync(string reservationId, CancellationToken ct);
}

public interface IShippingService
{
    Task<ShipmentInfo> CreateShipmentAsync(ShipmentRequest request, CancellationToken ct);
}

public interface INotificationService
{
    Task SendOrderConfirmationAsync(OrderConfirmationNotification notification, CancellationToken ct);
    Task SendCancellationNoticeAsync(OrderCancellationNotification notification, CancellationToken ct);
}

public interface ICustomerService
{
    Task<Customer> GetCustomerAsync(string customerId, CancellationToken ct);
}
```

## Registration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWolverine(opts =>
{
    opts.Durability.Mode = DurabilityMode.Solo;
});

builder.Services.AddMarten(opts =>
{
    opts.Connection(builder.Configuration.GetConnectionString("Marten")!);
})
.IntegrateWithWolverine();

// Register workflow
builder.Services.AddStrategos()
    .AddWorkflow<ProcessOrderWorkflow>()
    .AddWorkflow<CompensateOrderWorkflow>();

// Register services
builder.Services.AddScoped<IOrderValidator, AddressValidationService>();
builder.Services.AddScoped<IProductCatalog, ProductCatalogService>();
builder.Services.AddScoped<IPaymentGateway, StripePaymentGateway>();
builder.Services.AddScoped<IInventoryService, WarehouseInventoryService>();
builder.Services.AddScoped<IShippingService, ShipStationService>();
builder.Services.AddScoped<INotificationService, SendGridNotificationService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();

var app = builder.Build();
app.MapControllers();
app.Run();
```

## Starting the Workflow

```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IWorkflowStarter _workflowStarter;
    private readonly IDocumentSession _session;

    public OrdersController(
        IWorkflowStarter workflowStarter,
        IDocumentSession session)
    {
        _workflowStarter = workflowStarter;
        _session = session;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var workflowId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var order = new Order(
            OrderId: orderId,
            CustomerId: request.CustomerId,
            Items: request.Items.Select(i => new OrderItem(
                i.ProductId, i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
            ShippingAddress: request.ShippingAddress,
            BillingAddress: request.BillingAddress,
            PaymentMethod: request.PaymentMethod,
            TotalAmount: request.Items.Sum(i => i.Quantity * i.UnitPrice));

        var initialState = new OrderState
        {
            WorkflowId = workflowId,
            Order = order,
            EventHistory = [new OrderEvent("OrderCreated", DateTimeOffset.UtcNow,
                $"Order {orderId} created with {order.Items.Count} items")]
        };

        await _workflowStarter.StartAsync("process-order", initialState, ct);

        return Accepted(new
        {
            WorkflowId = workflowId,
            OrderId = orderId
        });
    }

    [HttpGet("{workflowId}")]
    public async Task<IActionResult> GetOrderStatus(Guid workflowId, CancellationToken ct)
    {
        var saga = await _session.LoadAsync<ProcessOrderSaga>(workflowId, ct);

        if (saga is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            WorkflowId = workflowId,
            OrderId = saga.State.Order.OrderId,
            Status = saga.State.Status.ToString(),
            Phase = saga.Phase.ToString(),
            TrackingNumber = saga.State.Shipment?.TrackingNumber,
            Events = saga.State.EventHistory.Select(e => new
            {
                e.EventType,
                e.Timestamp,
                e.Details
            })
        });
    }
}
```

## Generated Artifacts

The source generator produces these artifacts from the workflow definition:

### Phase Enum

```csharp
public enum ProcessOrderPhase
{
    NotStarted,
    ValidateOrder,
    ProcessPayment,
    ReserveInventory,
    CreateShipment,
    SendConfirmation,
    Completed,
    Failed
}
```

### Saga Class

```csharp
public class ProcessOrderSaga : Saga
{
    public Guid WorkflowId { get; set; }
    public OrderState State { get; set; } = null!;
    public ProcessOrderPhase Phase { get; set; }

    // Generated handlers for each step
    public async Task Handle(ExecuteValidateOrderCommand command, ...);
    public async Task Handle(ExecuteProcessPaymentCommand command, ...);
    // ... etc
}
```

### Commands

```csharp
public record StartProcessOrderCommand(Guid WorkflowId, OrderState InitialState);
public record ExecuteValidateOrderCommand(Guid WorkflowId);
public record ExecuteProcessPaymentCommand(Guid WorkflowId);
public record ExecuteReserveInventoryCommand(Guid WorkflowId);
public record ExecuteCreateShipmentCommand(Guid WorkflowId);
public record ExecuteSendConfirmationCommand(Guid WorkflowId);
```

### Events

```csharp
public record ProcessOrderStarted(Guid WorkflowId, DateTimeOffset Timestamp);
public record ProcessOrderPhaseChanged(Guid WorkflowId, ProcessOrderPhase Phase, DateTimeOffset Timestamp);
public record ProcessOrderCompleted(Guid WorkflowId, DateTimeOffset Timestamp);
public record ProcessOrderFailed(Guid WorkflowId, string ErrorCode, string ErrorMessage, DateTimeOffset Timestamp);
```

## Key Points

- **Linear execution** with clear step dependencies
- **Idempotent steps** check for prior completion before re-executing
- **Immutable state** with `With()` for updates
- **Event history** via `[Append]` reducer for audit trail
- **Compensation workflow** handles rollback on failure
- **Realistic domain model** with proper value objects
- **Service abstraction** via interfaces for testability
- **Full error handling** with typed error codes

## Related Documentation

- [Basic Workflow Example](/strategos/examples/basic-workflow/) - Simpler linear workflow introduction
- [Branching Example](/strategos/examples/branching/) - Conditional execution paths
- [State Management](/guide/state-management) - Deep dive on state patterns
