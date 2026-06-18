// -----------------------------------------------------------------------
// <copyright file="CompensationWorkflow.cs" company="Levelup Software">
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
/// Immutable state for the compensation behavioral fixture (DR-3).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer used by the saga to fold each step's returned state.
/// </remarks>
[WorkflowState]
public sealed record CompensationState : IWorkflowState
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
    /// Gets a value indicating whether the compensation (rollback) ran.
    /// </summary>
    public bool RolledBack { get; init; }
}

/// <summary>
/// Permanent failure raised by <see cref="CompensatedFailingStep"/> on EVERY
/// invocation, so the generated <c>.WithRetry(2)</c> policy exhausts its retries
/// and the lowered compensation path fires.
/// </summary>
public sealed class CompensatedStepException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompensatedStepException"/>
    /// class with the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CompensatedStepException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Entry step of the compensation fixture workflow. Deterministic (never throws);
/// records its invocation so the test can confirm the saga actually started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CompensationPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<CompensationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensationState>> ExecuteAsync(
        CompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CompensationPrepareStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompensationState>.FromState(updated));
    }
}

/// <summary>
/// The compensated middle step. It declares <c>.WithRetry(2).Compensate&lt;RollbackStep&gt;()</c>
/// and throws on EVERY invocation, so Wolverine retries it three times total
/// (initial + two retries) and then the lowered compensation path publishes the
/// trigger that runs <see cref="RollbackStep"/>. Records every attempt so the
/// test can prove the retries happened before compensation.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CompensatedFailingStep(WorkflowInvocationLog log) : IWorkflowStep<CompensationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensationState>> ExecuteAsync(
        CompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CompensatedFailingStep));

        throw new CompensatedStepException(
            "CompensatedFailingStep always fails to force retry exhaustion and compensation.");
    }
}

/// <summary>
/// The compensation (rollback) step run when <see cref="CompensatedFailingStep"/>
/// exhausts its retries. Records its invocation exactly once per failed step so
/// the test can assert the compensation ran exactly once. Per INV-7 it returns
/// NEW state (sets <see cref="CompensationState.RolledBack"/>) rather than
/// mutating the input.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class RollbackStep(WorkflowInvocationLog log) : IWorkflowStep<CompensationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensationState>> ExecuteAsync(
        CompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(RollbackStep));

        var updated = state with { RolledBack = true };
        return Task.FromResult(StepResult<CompensationState>.FromState(updated));
    }
}

/// <summary>
/// A step that must NOT run when the preceding <see cref="CompensatedFailingStep"/>
/// fails: once compensation fires, the saga routes to Failed and
/// <c>MarkCompleted()</c> before ever cascading this step's start command. Its
/// invocation count being zero is the observable proof that the failure routed
/// away from the happy path.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CompensationNeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<CompensationState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensationState>> ExecuteAsync(
        CompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CompensationNeverReachedStep));

        var updated = state with { StepCount = state.StepCount + 1 };
        return Task.FromResult(StepResult<CompensationState>.FromState(updated));
    }
}

/// <summary>
/// The compensation-proof fixture workflow definition. The
/// <see cref="WorkflowAttribute"/> drives the Strategos source generator to emit,
/// at build time, the Wolverine saga (<c>CompensationProofSaga</c>), the worker
/// handler for <see cref="CompensatedFailingStep"/> carrying the generated
/// <c>Configure(HandlerChain)</c> retry + compensation policy (DR-3), the worker
/// handler for <see cref="RollbackStep"/> (folded in so it can RUN), the saga
/// compensation chain, the start command (<c>StartCompensationProofCommand</c>),
/// and the <c>AddCompensationProofWorkflow()</c> DI extension consumed by the host
/// fixture.
/// </summary>
[Workflow("compensation-proof")]
public static partial class CompensationProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: a prepare step, a middle step
    /// declaring <c>.WithRetry(2).Compensate&lt;RollbackStep&gt;()</c> that always
    /// throws, and a terminal step that must never be reached.
    /// </summary>
    public static WorkflowDefinition<CompensationState> Definition => Workflow<CompensationState>
        .Create("compensation-proof")
        .StartWith<CompensationPrepareStep>()
        .Then<CompensatedFailingStep>(step => step
            .WithRetry(2)
            .Compensate<RollbackStep>())
        .Finally<CompensationNeverReachedStep>();
}
