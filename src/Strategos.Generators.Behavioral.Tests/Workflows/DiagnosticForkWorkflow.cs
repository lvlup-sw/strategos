// -----------------------------------------------------------------------
// <copyright file="DiagnosticForkWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Agents.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Steps;

using ForkTrigger = Strategos.Contracts.Generated.ForkTrigger;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>
/// Immutable, event-sourced state for the diagnostic-fork lowering proof (DR-9, #151).
/// Implements <see cref="IEventSourcedState{TState}"/> so the EventSourced-mode saga
/// applies each appended event; the <c>Apply</c> overload lets Marten's inline snapshot
/// projection build for the registration.
/// </summary>
public sealed record DiagnosticForkState : IEventSourcedState<DiagnosticForkState>
{
    /// <summary>
    /// Gets the Marten aggregate identity (the event stream id, equal to
    /// <see cref="WorkflowId"/>).
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the unique identifier for this workflow instance.
    /// </summary>
    public Guid WorkflowId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the fork's seeded compensation (rollback) ran.
    /// </summary>
    public bool RolledBack { get; init; }

    /// <summary>
    /// Marten aggregation fold for the anchor step's completed event. The WorkflowForked
    /// audit event has no <c>Apply</c> overload, so Marten's inline aggregation tolerates
    /// and skips it while it still lands in the raw stream the fork proof reads.
    /// </summary>
    /// <param name="evt">The anchor-step completed event.</param>
    /// <returns>The aggregate seeded with the stream id.</returns>
    public DiagnosticForkState Apply(DfAnchorStepCompleted evt)
    {
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));
        return this with { Id = evt.WorkflowId, WorkflowId = evt.WorkflowId };
    }

    /// <inheritdoc />
    public DiagnosticForkState ApplyEvent(IProgressEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt, nameof(evt));
        return this;
    }
}

/// <summary>
/// Entry / anchor step of the diagnostic-fork fixture. Deterministic; records its
/// invocation. It is the fork edge's anchor moniker.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class DfAnchorStep(WorkflowInvocationLog log) : IWorkflowStep<DiagnosticForkState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<DiagnosticForkState>> ExecuteAsync(
        DiagnosticForkState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(DfAnchorStep));
        return Task.FromResult(StepResult<DiagnosticForkState>.FromState(state));
    }
}

/// <summary>
/// The compensated step (declares <c>.Compensate&lt;DfRollbackStep&gt;()</c>). It is the
/// fork's compensation seed: a valid fork routes its rollback here through the merged
/// Compensate/OnFailure trigger site (#140). Deterministic — the fork, not this step's
/// failure, drives the rollback.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class DfStampStep(WorkflowInvocationLog log) : IWorkflowStep<DiagnosticForkState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<DiagnosticForkState>> ExecuteAsync(
        DiagnosticForkState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(DfStampStep));
        return Task.FromResult(StepResult<DiagnosticForkState>.FromState(state));
    }
}

/// <summary>
/// The rollback (compensation) step the fork seeds. Runs when a valid fork routes its
/// compensation seed into the merged trigger site. Per INV-7 returns NEW state (sets
/// <see cref="DiagnosticForkState.RolledBack"/>).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class DfRollbackStep(WorkflowInvocationLog log) : IWorkflowStep<DiagnosticForkState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<DiagnosticForkState>> ExecuteAsync(
        DiagnosticForkState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(DfRollbackStep));

        var updated = state with { RolledBack = true };
        return Task.FromResult(StepResult<DiagnosticForkState>.FromState(updated));
    }
}

/// <summary>
/// Terminal step, present to satisfy the generator's <c>Finally&lt;T&gt;()</c> terminator
/// requirement. Not exercised by the fork proofs (they drive the saga through the fork
/// decision command, not the happy path).
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class DfSettleStep(WorkflowInvocationLog log) : IWorkflowStep<DiagnosticForkState>
{
    private readonly WorkflowInvocationLog log = log;

    /// <inheritdoc />
    public Task<StepResult<DiagnosticForkState>> ExecuteAsync(
        DiagnosticForkState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        this.log.Record(nameof(DfSettleStep));
        return Task.FromResult(StepResult<DiagnosticForkState>.FromState(state));
    }
}

/// <summary>
/// The diagnostic-fork lowering fixture workflow (DR-9, #151). Event-sourced so the
/// generated saga compiles the <c>{Pascal}WorkflowForked</c> audit-event append; declares
/// a compensated step so the fork's compensation seed composes with the merged trigger
/// site (#140); and declares an <c>AllowDiagnosticFork</c> edge anchored at
/// <see cref="DfAnchorStep"/> permitting two triggers, seeding compensation to
/// <see cref="DfStampStep"/>, and bounding forks at 1 so the maxForks guard is reachable.
/// Drives the generator to emit <c>DiagnosticForkProofSaga</c>,
/// <c>ForkDiagnosticForkProofCommand</c>, <c>DiagnosticForkProofWorkflowForked</c>, and
/// <c>AddDiagnosticForkProofWorkflow()</c>.
/// </summary>
[Workflow("diagnostic-fork-proof", Persistence = PersistenceMode.EventSourced)]
public static partial class DiagnosticForkProofWorkflowDefinition
{
    /// <summary>
    /// Gets the fluent definition: an anchor step, a compensated step, a diagnostic-fork
    /// edge (anchor + two permitted triggers + compensation seed + maxForks bound of 1),
    /// and a terminal step.
    /// </summary>
    public static WorkflowDefinition<DiagnosticForkState> Definition => Workflow<DiagnosticForkState>
        .Create("diagnostic-fork-proof")
        .StartWith<DfAnchorStep>()
        .Then<DfStampStep>(step => step.Compensate<DfRollbackStep>())
        .AllowDiagnosticFork(fork => fork
            .Anchor("DfAnchorStep")
            .PermitTrigger(ForkTrigger.RatificationFailure, "provisionalStampEventId", "taints")
            .PermitTrigger(ForkTrigger.GateContradiction, "leftGateId", "rightGateId")
            .WithCompensationSeed("DfStampStep")
            .MaxForks(1))
        .Finally<DfSettleStep>();
}
