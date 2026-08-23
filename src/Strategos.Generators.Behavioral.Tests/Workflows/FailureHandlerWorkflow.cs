// -----------------------------------------------------------------------
// <copyright file="FailureHandlerWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable state for the OnFailure-chain behavioral fixture (#140 Task 3.1).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by the saga to fold each step's returned state, including the
/// state returned by the workflow-level <c>OnFailure</c> handler step.
/// </remarks>
[WorkflowState]
public sealed record FailureHandlerState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the OnFailure handler step ran.
    /// </summary>
    public bool FailureNotified { get; init; }
}

/// <summary>
/// Permanent failure raised by <see cref="FailureHandlerFailingStep"/> on EVERY
/// invocation, so the saga routes to the workflow-level <c>OnFailure</c> chain.
/// </summary>
public sealed class FailureHandlerStepException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FailureHandlerStepException"/>
    /// class with the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public FailureHandlerStepException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Entry step of the OnFailure-chain fixture workflow. Deterministic (never
/// throws); records its invocation so the test can confirm the saga started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailureHandlerPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<FailureHandlerState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<FailureHandlerState>> ExecuteAsync(
        FailureHandlerState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailureHandlerPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<FailureHandlerState>.FromState(updated));
    }
}

/// <summary>
/// The failing middle step. It declares NO step-level resilience (no retry, no
/// compensation) and throws on EVERY invocation, so the saga must route to the
/// workflow-level <c>OnFailure</c> handler chain.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailureHandlerFailingStep(WorkflowInvocationLog log) : IWorkflowStep<FailureHandlerState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<FailureHandlerState>> ExecuteAsync(
        FailureHandlerState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailureHandlerFailingStep));

        throw new FailureHandlerStepException(
            "FailureHandlerFailingStep always fails to force the workflow OnFailure chain.");
    }
}

/// <summary>
/// The workflow-level <c>OnFailure</c> handler step run when
/// <see cref="FailureHandlerFailingStep"/> fails. Records its invocation exactly
/// once so the test can assert the dead OnFailure chain now actually runs. Per
/// INV-7 it returns NEW state (sets <see cref="FailureHandlerState.FailureNotified"/>)
/// rather than mutating the input.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class NotifyFailure(WorkflowInvocationLog log) : IWorkflowStep<FailureHandlerState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<FailureHandlerState>> ExecuteAsync(
        FailureHandlerState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(NotifyFailure));

        var updated = state with { FailureNotified = true };
        return Task.FromResult(StepResult<FailureHandlerState>.FromState(updated));
    }
}

/// <summary>
/// A step that must NOT run when the preceding <see cref="FailureHandlerFailingStep"/>
/// fails: once the saga routes to the OnFailure chain it reaches Failed before
/// ever cascading this step. Its invocation count being zero is the observable
/// proof that the failure routed away from the happy path.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class FailureHandlerNeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<FailureHandlerState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<FailureHandlerState>> ExecuteAsync(
        FailureHandlerState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(FailureHandlerNeverReachedStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<FailureHandlerState>.FromState(updated));
    }
}

/// <summary>
/// The OnFailure-chain proof fixture workflow definition (#140 Task 3.1). The
/// <see cref="WorkflowAttribute"/> drives the Strategos source generator to emit,
/// at build time, the Wolverine saga, the worker handlers for every step
/// (including the <c>OnFailure</c> handler step <see cref="NotifyFailure"/>), the
/// failure-handler trigger/start/worker commands, the failure-handler completed
/// event, and the <c>AddFailureHandlerProofWorkflow()</c> DI extension consumed
/// by the host fixture.
/// </summary>
[Workflow("failure-handler-proof")]
public static partial class FailureHandlerProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: a prepare step, a middle step that
    /// always throws with NO step-level resilience, a terminal step that must
    /// never be reached, and a workflow-level <c>OnFailure</c> chain that runs
    /// <see cref="NotifyFailure"/>.
    /// </summary>
    public static WorkflowDefinition<FailureHandlerState> Definition => Workflow<FailureHandlerState>
        .Create("failure-handler-proof")
        .StartWith<FailureHandlerPrepareStep>()
        .Then<FailureHandlerFailingStep>()
        .OnFailure(flow => flow.Then<NotifyFailure>().Complete())
        .Finally<FailureHandlerNeverReachedStep>();
}

// =============================================================================
// The SUCCESS-PATH twin of the fixture above.
//
// The proof above deliberately never reaches its terminal: its middle step always
// throws, so the saga routes into the OnFailure chain and the terminal step's
// invocation count STAYING AT ZERO is the assertion. That leaves the opposite case
// unproven — a workflow that declares an OnFailure chain, never fails, and must
// therefore run straight through its terminal to completion.
//
// It is not the same code path. A workflow-level OnFailure lowers its handler steps
// as extra entries appended to the workflow's step-name list AFTER the declared
// terminal, so the terminal is no longer the last entry. A successor scan that reads
// "last" off list position hands the terminal the appended handler step as its
// successor: on the success path the terminal cascades into the failure handler and
// the saga never completes. Only a run that actually reaches the terminal can observe
// that, which is what this fixture is for.
//
// Every type below is new rather than shared with the fixture above: two [Workflow]
// definitions in one compilation may not name the same step CLR type, because the
// generated per-step handler classes would collide (CS0101).
// =============================================================================

/// <summary>
/// Immutable state for the success-path order-fulfillment fixture.
/// </summary>
/// <remarks>
/// Carries a per-step counter so the folded state is observable independently of the
/// invocation log, plus a flag the refund handler sets — which must stay
/// <see langword="false"/> on a run where nothing fails.
/// </remarks>
[WorkflowState]
public sealed record OrderFulfillmentState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets the number of steps that have folded their result into state.
    /// </summary>
    public int StepCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the charge was refunded by the failure handler.
    /// </summary>
    public bool ChargeRefunded { get; init; }
}

/// <summary>
/// Entry step of the success-path fixture. Deterministic: it never throws.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ReserveInventory(WorkflowInvocationLog log) : IWorkflowStep<OrderFulfillmentState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<OrderFulfillmentState>> ExecuteAsync(
        OrderFulfillmentState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ReserveInventory));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<OrderFulfillmentState>.FromState(updated));
    }
}

/// <summary>
/// The middle step. It is the step the workflow-level <c>OnFailure</c> chain guards,
/// and — unlike its counterpart in the fixture above — it succeeds on every
/// invocation, so the run stays on the main flow all the way to the terminal.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ChargeCustomer(WorkflowInvocationLog log) : IWorkflowStep<OrderFulfillmentState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<OrderFulfillmentState>> ExecuteAsync(
        OrderFulfillmentState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ChargeCustomer));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<OrderFulfillmentState>.FromState(updated));
    }
}

/// <summary>
/// The workflow-level <c>OnFailure</c> handler step. Nothing in this fixture ever
/// fails, so its invocation count staying at zero is the observable proof that the
/// terminal completed the saga instead of cascading into the appended handler chain.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RefundCharge(WorkflowInvocationLog log) : IWorkflowStep<OrderFulfillmentState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<OrderFulfillmentState>> ExecuteAsync(
        OrderFulfillmentState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RefundCharge));

        var updated = state with { ChargeRefunded = true };
        return Task.FromResult(StepResult<OrderFulfillmentState>.FromState(updated));
    }
}

/// <summary>
/// The declared terminal step. Reaching it exactly once, and having the saga document
/// removed afterwards, is what this fixture exists to demonstrate.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ConfirmShipment(WorkflowInvocationLog log) : IWorkflowStep<OrderFulfillmentState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<OrderFulfillmentState>> ExecuteAsync(
        OrderFulfillmentState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(ConfirmShipment));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<OrderFulfillmentState>.FromState(updated));
    }
}

/// <summary>
/// The success-path order-fulfillment workflow definition: a linear main flow guarded
/// by a workflow-level <c>OnFailure</c> chain that is never triggered.
/// </summary>
/// <remarks>
/// The <see cref="WorkflowAttribute"/> drives the source generator to emit the saga,
/// the per-step worker handlers, the failure-handler trigger/start/worker commands for
/// <see cref="RefundCharge"/>, and the <c>AddOrderFulfillmentWorkflow()</c> DI
/// extension the host fixture registers.
/// </remarks>
[Workflow("order-fulfillment")]
public static partial class OrderFulfillmentWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: reserve, charge, confirm — with a refund
    /// handler declared for the failure that never happens.
    /// </summary>
    public static WorkflowDefinition<OrderFulfillmentState> Definition => Workflow<OrderFulfillmentState>
        .Create("order-fulfillment")
        .StartWith<ReserveInventory>()
        .Then<ChargeCustomer>()
        .OnFailure(flow => flow.Then<RefundCharge>().Complete())
        .Finally<ConfirmShipment>();
}
