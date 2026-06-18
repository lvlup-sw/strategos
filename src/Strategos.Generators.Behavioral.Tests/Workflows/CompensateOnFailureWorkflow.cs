// -----------------------------------------------------------------------
// <copyright file="CompensateOnFailureWorkflow.cs" company="Levelup Software">
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
/// Immutable state for the Compensate↔OnFailure interop behavioral fixture
/// (#140 Task 3.2).
/// </summary>
/// <remarks>
/// Marked <see cref="WorkflowStateAttribute"/> so the source generator emits a
/// reducer the saga uses to fold both the compensation step's and the OnFailure
/// handler step's returned state.
/// </remarks>
[WorkflowState]
public sealed record CompensateOnFailureState : IWorkflowState
{
    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the compensation (rollback) ran.
    /// </summary>
    public bool RolledBack { get; init; }

    /// <summary>
    /// Gets a value indicating whether the OnFailure handler step ran.
    /// </summary>
    public bool FailureNotified { get; init; }
}

/// <summary>
/// Permanent failure raised by <see cref="CofFailingStep"/> on EVERY invocation,
/// so the saga runs both the step compensation and the workflow OnFailure chain.
/// </summary>
public sealed class CofStepException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CofStepException"/> class with
    /// the supplied message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CofStepException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Entry step of the interop fixture workflow. Deterministic (never throws);
/// records its invocation so the test can confirm the saga started.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CofPrepareStep(WorkflowInvocationLog log) : IWorkflowStep<CompensateOnFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensateOnFailureState>> ExecuteAsync(
        CompensateOnFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CofPrepareStep));

        return Task.FromResult(StepResult<CompensateOnFailureState>.FromState(state));
    }
}

/// <summary>
/// The compensated middle step. Declares <c>.Compensate&lt;CofRollbackStep&gt;()</c>
/// and throws on EVERY invocation, so the saga runs the rollback FIRST, then the
/// workflow OnFailure chain (#140 Task 3.2 fixed ordering).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CofFailingStep(WorkflowInvocationLog log) : IWorkflowStep<CompensateOnFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensateOnFailureState>> ExecuteAsync(
        CompensateOnFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CofFailingStep));

        throw new CofStepException(
            "CofFailingStep always fails to force compensation then OnFailure.");
    }
}

/// <summary>
/// The compensation (rollback) step. Runs FIRST when <see cref="CofFailingStep"/>
/// fails. Per INV-7 returns NEW state (sets
/// <see cref="CompensateOnFailureState.RolledBack"/>) rather than mutating input.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CofRollbackStep(WorkflowInvocationLog log) : IWorkflowStep<CompensateOnFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensateOnFailureState>> ExecuteAsync(
        CompensateOnFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CofRollbackStep));

        var updated = state with { RolledBack = true };
        return Task.FromResult(StepResult<CompensateOnFailureState>.FromState(updated));
    }
}

/// <summary>
/// The workflow-level OnFailure handler step. Runs AFTER the compensation rollback
/// (#140 Task 3.2 fixed ordering). Per INV-7 returns NEW state (sets
/// <see cref="CompensateOnFailureState.FailureNotified"/>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CofNotifyFailure(WorkflowInvocationLog log) : IWorkflowStep<CompensateOnFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensateOnFailureState>> ExecuteAsync(
        CompensateOnFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CofNotifyFailure));

        var updated = state with { FailureNotified = true };
        return Task.FromResult(StepResult<CompensateOnFailureState>.FromState(updated));
    }
}

/// <summary>
/// A step that must NOT run when <see cref="CofFailingStep"/> fails. Its
/// invocation count being zero proves the failure routed away from the happy path.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class CofNeverReachedStep(WorkflowInvocationLog log) : IWorkflowStep<CompensateOnFailureState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<CompensateOnFailureState>> ExecuteAsync(
        CompensateOnFailureState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(CofNeverReachedStep));

        return Task.FromResult(StepResult<CompensateOnFailureState>.FromState(state));
    }
}

/// <summary>
/// The Compensate↔OnFailure interop fixture workflow definition (#140 Task 3.2).
/// Declares BOTH a step-level <c>.Compensate&lt;CofRollbackStep&gt;()</c> AND a
/// workflow-level <c>OnFailure</c> chain, which were previously mutually exclusive.
/// The fixed-order contract: step compensation runs FIRST, then the OnFailure chain.
/// </summary>
[Workflow("compensate-on-failure-proof")]
public static partial class CompensateOnFailureProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent workflow definition: a prepare step, a middle step declaring
    /// <c>.Compensate&lt;CofRollbackStep&gt;()</c> that always throws, a terminal
    /// step that must never be reached, and a workflow-level <c>OnFailure</c> chain
    /// that runs <see cref="CofNotifyFailure"/>.
    /// </summary>
    public static WorkflowDefinition<CompensateOnFailureState> Definition => Workflow<CompensateOnFailureState>
        .Create("compensate-on-failure-proof")
        .StartWith<CofPrepareStep>()
        .Then<CofFailingStep>(step => step.Compensate<CofRollbackStep>())
        .OnFailure(flow => flow.Then<CofNotifyFailure>().Complete())
        .Finally<CofNeverReachedStep>();
}
