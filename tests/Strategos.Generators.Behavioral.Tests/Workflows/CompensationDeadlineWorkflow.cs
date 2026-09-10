// -----------------------------------------------------------------------
// <copyright file="CompensationDeadlineWorkflow.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Abstractions;
using Strategos.Attributes;
using Strategos.Builders;
using Strategos.Definitions;
using Strategos.Generators.Behavioral.Tests.Infrastructure;
using Strategos.Ontology;
using Strategos.Ontology.Builder;
using Strategos.Steps;

namespace Strategos.Generators.Behavioral.Tests.Workflows;

/// <summary>Ontology-only subject used to prove the authored inverse deadline.</summary>
public sealed class CompensationDeadlineOrder
{
    /// <summary>Gets or sets the logical stage used by the action contracts.</summary>
    public int Stage { get; set; }
}

/// <summary>Closed action catalog for the inverse-deadline proof.</summary>
public sealed class CompensationDeadlineOntology : DomainOntology
{
    /// <inheritdoc />
    public override string DomainName => "compensation-deadline";

    /// <inheritdoc />
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<CompensationDeadlineOrder>("Order", obj =>
        {
            obj.Action("program")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage)
                .BoundToWorkflow("compensation-deadline-proof");
            obj.Action("a")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
            obj.Action("undo-a")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Stage == 0)
                .Modifies(order => order.Stage);
            obj.Action("b")
                .Requires(order => order.Stage == 1)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage);
            obj.Action("undo-b")
                .Requires(order => order.Stage == 2)
                .Ensures(order => order.Stage == 1)
                .Modifies(order => order.Stage);
        });
    }
}

/// <summary>Immutable state for the inverse-deadline proof.</summary>
[WorkflowState]
public sealed record CompensationDeadlineState : IWorkflowState
{
    /// <summary>Gets the workflow identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the state-machine stage folded by generated saga handlers.</summary>
    public int Stage { get; init; }
}

/// <summary>Completes the only forward transition that can be rolled back.</summary>
public sealed class DeadlineForwardStep(WorkflowInvocationLog log)
    : IWorkflowStep<CompensationDeadlineState>
{
    /// <inheritdoc />
    public Task<StepResult<CompensationDeadlineState>> ExecuteAsync(
        CompensationDeadlineState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        log.Record(nameof(DeadlineForwardStep));
        return Task.FromResult(StepResult<CompensationDeadlineState>.FromState(
            state with { Stage = 1 }));
    }
}

/// <summary>Fails so the completed prefix is rolled back.</summary>
public sealed class DeadlineFailingStep(WorkflowInvocationLog log)
    : IWorkflowStep<CompensationDeadlineState>
{
    /// <inheritdoc />
    public Task<StepResult<CompensationDeadlineState>> ExecuteAsync(
        CompensationDeadlineState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        log.Record(nameof(DeadlineFailingStep));
        throw new CompensatedStepException(
            "Fails so the authored inverse deadline governs the rollback.");
    }
}

/// <summary>
/// The inverse of <see cref="DeadlineForwardStep"/>, deliberately slower than the authored
/// one-second deadline so the generated rollback timeout fires while it is still in flight.
/// </summary>
public sealed class DeadlineSlowUndoStep(WorkflowInvocationLog log)
    : IWorkflowStep<CompensationDeadlineState>
{
    /// <summary>
    /// The inverse execution's duration. Chosen far above the authored one-second deadline
    /// and far below the generated default deadline, so the observed outcome distinguishes
    /// the two.
    /// </summary>
    public static readonly TimeSpan InverseDuration = TimeSpan.FromSeconds(20);

    /// <inheritdoc />
    public async Task<StepResult<CompensationDeadlineState>> ExecuteAsync(
        CompensationDeadlineState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        log.Record(nameof(DeadlineSlowUndoStep));
        await Task.Delay(InverseDuration, cancellationToken);
        return StepResult<CompensationDeadlineState>.FromState(state with { Stage = 0 });
    }
}

/// <summary>The failing step's inverse; never runs because the forward never completed.</summary>
public sealed class DeadlineUnreachedUndoStep(WorkflowInvocationLog log)
    : IWorkflowStep<CompensationDeadlineState>
{
    /// <inheritdoc />
    public Task<StepResult<CompensationDeadlineState>> ExecuteAsync(
        CompensationDeadlineState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        log.Record(nameof(DeadlineUnreachedUndoStep));
        return Task.FromResult(StepResult<CompensationDeadlineState>.FromState(
            state with { Stage = 1 }));
    }
}

/// <summary>
/// Typed workflow whose first occurrence authors an explicit ONE SECOND inverse deadline
/// through <c>Compensate&lt;T&gt;(inverseAction, timeout)</c>. Its inverse takes twenty
/// seconds, so the authored deadline — not the generated default — decides the outcome.
/// </summary>
[Workflow("compensation-deadline-proof")]
public static partial class CompensationDeadlineProofWorkflowDefinition
{
    /// <summary>The authored deadline for one inverse execution.</summary>
    public static readonly TimeSpan AuthoredInverseDeadline = TimeSpan.FromSeconds(1);

    /// <summary>Gets the closed typed workflow definition.</summary>
    public static WorkflowDefinition<CompensationDeadlineState> Definition =>
        Workflow<CompensationDeadlineState>
            .Create("compensation-deadline-proof")
            .StartWith<DeadlineForwardStep>(step => step
                .Performs(new WorkflowActionReference(
                    "compensation-deadline", "Order", "a"))
                .Compensate<DeadlineSlowUndoStep>(
                    new WorkflowActionReference("compensation-deadline", "Order", "undo-a"),
                    TimeSpan.FromSeconds(1)))
            .Finally<DeadlineFailingStep>(step => step
                .Performs(new WorkflowActionReference(
                    "compensation-deadline", "Order", "b"))
                .Compensate<DeadlineUnreachedUndoStep>(new WorkflowActionReference(
                    "compensation-deadline", "Order", "undo-b")));
}
