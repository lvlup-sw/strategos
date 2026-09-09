// -----------------------------------------------------------------------
// <copyright file="DerivedCompensationWorkflow.cs" company="Levelup Software">
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

/// <summary>Ontology-only subject used to prove the derived compensation fixture.</summary>
public sealed class DerivedCompensationOrder
{
    /// <summary>Gets or sets the logical stage used by the action contracts.</summary>
    public int Stage { get; set; }
}

/// <summary>Closed action catalog for the typed completed-prefix rollback proof.</summary>
public sealed class DerivedCompensationOntology : DomainOntology
{
    /// <inheritdoc />
    public override string DomainName => "derived-compensation";

    /// <inheritdoc />
    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<DerivedCompensationOrder>("Order", obj =>
        {
            obj.Action("program")
                .Requires(order => order.Stage == 0)
                .Ensures(order => order.Stage == 3)
                .Modifies(order => order.Stage)
                .BoundToWorkflow("derived-compensation-proof");
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
            obj.Action("c")
                .Requires(order => order.Stage == 2)
                .Ensures(order => order.Stage == 3)
                .Modifies(order => order.Stage);
            obj.Action("undo-c")
                .Requires(order => order.Stage == 3)
                .Ensures(order => order.Stage == 2)
                .Modifies(order => order.Stage);
        });
    }
}

/// <summary>Immutable state for the typed completed-prefix rollback proof.</summary>
[WorkflowState]
public sealed record DerivedCompensationState : IWorkflowState
{
    /// <summary>Gets the workflow identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the state-machine stage folded by generated saga handlers.</summary>
    public int Stage { get; init; }
}

/// <summary>Completes the first forward transition.</summary>
public sealed class DerivedForwardAStep(WorkflowInvocationLog log, StepExecutionIdProbe probe)
    : IWorkflowStep<DerivedCompensationState>
{
    /// <inheritdoc />
    public Task<StepResult<DerivedCompensationState>> ExecuteAsync(
        DerivedCompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        probe.Record(nameof(DerivedForwardAStep), context);
        log.Record(nameof(DerivedForwardAStep));
        return Task.FromResult(StepResult<DerivedCompensationState>.FromState(
            state with { Stage = 1 }));
    }
}

/// <summary>Completes the second forward transition.</summary>
public sealed class DerivedForwardBStep(WorkflowInvocationLog log, StepExecutionIdProbe probe)
    : IWorkflowStep<DerivedCompensationState>
{
    /// <inheritdoc />
    public Task<StepResult<DerivedCompensationState>> ExecuteAsync(
        DerivedCompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        if (state.Stage != 1)
        {
            throw new InvalidOperationException("Forward B did not receive A's reduced state.");
        }

        probe.Record(nameof(DerivedForwardBStep), context);
        log.Record(nameof(DerivedForwardBStep));
        return Task.FromResult(StepResult<DerivedCompensationState>.FromState(
            state with { Stage = 2 }));
    }
}

/// <summary>Fails after the first two forward transitions have completed.</summary>
public sealed class DerivedFailingCStep(WorkflowInvocationLog log, StepExecutionIdProbe probe)
    : IWorkflowStep<DerivedCompensationState>
{
    /// <inheritdoc />
    public Task<StepResult<DerivedCompensationState>> ExecuteAsync(
        DerivedCompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        if (state.Stage != 2)
        {
            throw new InvalidOperationException("Forward C did not receive B's reduced state.");
        }

        probe.Record(nameof(DerivedFailingCStep), context);
        log.Record(nameof(DerivedFailingCStep));
        throw new CompensatedStepException("C fails so rollback must use only the completed A/B prefix.");
    }
}

/// <summary>Restores the state required before forward A.</summary>
public sealed class DerivedUndoAStep(WorkflowInvocationLog log, StepExecutionIdProbe probe)
    : IWorkflowStep<DerivedCompensationState>
{
    /// <inheritdoc />
    public Task<StepResult<DerivedCompensationState>> ExecuteAsync(
        DerivedCompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        if (state.Stage != 1)
        {
            throw new InvalidOperationException("Undo A did not receive Undo B's reduced state.");
        }

        probe.Record(nameof(DerivedUndoAStep), context);
        log.Record(nameof(DerivedUndoAStep));
        return Task.FromResult(StepResult<DerivedCompensationState>.FromState(
            state with { Stage = 0 }));
    }
}

/// <summary>Restores the state required before forward B.</summary>
public sealed class DerivedUndoBStep(WorkflowInvocationLog log, StepExecutionIdProbe probe)
    : IWorkflowStep<DerivedCompensationState>
{
    /// <inheritdoc />
    public Task<StepResult<DerivedCompensationState>> ExecuteAsync(
        DerivedCompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        if (state.Stage != 2)
        {
            throw new InvalidOperationException("Undo B did not receive the failed prefix state.");
        }

        probe.Record(nameof(DerivedUndoBStep), context);
        log.Record(nameof(DerivedUndoBStep));
        return Task.FromResult(StepResult<DerivedCompensationState>.FromState(
            state with { Stage = 1 }));
    }
}

/// <summary>Would restore C, but must not run because C never completed.</summary>
public sealed class DerivedUndoCStep(WorkflowInvocationLog log, StepExecutionIdProbe probe)
    : IWorkflowStep<DerivedCompensationState>
{
    /// <inheritdoc />
    public Task<StepResult<DerivedCompensationState>> ExecuteAsync(
        DerivedCompensationState state,
        StepContext context,
        CancellationToken cancellationToken)
    {
        probe.Record(nameof(DerivedUndoCStep), context);
        log.Record(nameof(DerivedUndoCStep));
        return Task.FromResult(StepResult<DerivedCompensationState>.FromState(
            state with { Stage = 2 }));
    }
}

/// <summary>Typed workflow whose runtime failure proves completed-prefix reversal.</summary>
[Workflow("derived-compensation-proof")]
public static partial class DerivedCompensationProofWorkflowDefinition
{
    /// <summary>Gets the closed typed workflow definition.</summary>
    public static WorkflowDefinition<DerivedCompensationState> Definition =>
        Workflow<DerivedCompensationState>
            .Create("derived-compensation-proof")
            .StartWith<DerivedForwardAStep>(step => step
                .Performs(new WorkflowActionReference(
                    "derived-compensation", "Order", "a"))
                .Compensate<DerivedUndoAStep>(new WorkflowActionReference(
                    "derived-compensation", "Order", "undo-a")))
            .Then<DerivedForwardBStep>(step => step
                .Performs(new WorkflowActionReference(
                    "derived-compensation", "Order", "b"))
                .Compensate<DerivedUndoBStep>(new WorkflowActionReference(
                    "derived-compensation", "Order", "undo-b")))
            .Finally<DerivedFailingCStep>(step => step
                .Performs(new WorkflowActionReference(
                    "derived-compensation", "Order", "c"))
                .Compensate<DerivedUndoCStep>(new WorkflowActionReference(
                    "derived-compensation", "Order", "undo-c")));
}
