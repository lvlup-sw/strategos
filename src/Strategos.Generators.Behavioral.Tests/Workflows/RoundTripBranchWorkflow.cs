// -----------------------------------------------------------------------
// <copyright file="RoundTripBranchWorkflow.cs" company="Levelup Software">
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

// =============================================================================
// An order-routing workflow whose branch cases BOTH rejoin the main flow:
//
//     ValidateOrder
//       ├── Channel == "Retail"  → ProcessRetailOrder    ─┐
//       └── otherwise            → ProcessWholesaleOrder ─┤
//                                                         └→ ShipOrder (terminal)
//
// The exclusive paths make the shape observable: for a given run exactly one of
// the two process steps may run, the other must not run at all, and ShipOrder
// must run exactly once. On the current generator this workflow does NOT
// terminate — the terminal's completed handler cascades back into a branch-path
// step, which rejoins at the terminal, so the workflow cycles and the Marten
// saga document is never deleted (#175). The count on the terminal is what
// separates completion from a cycle that was externally killed; a document that
// is merely absent cannot.
// =============================================================================

/// <summary>State for the rejoining branch workflow: an order routed by sales channel.</summary>
[WorkflowState]
public sealed record RoundTripBranchState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the sales channel that selects the branch case.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>Pre-branch step: validates the order before it is routed by channel.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ValidateOrder(WorkflowInvocationLog log) : IWorkflowStep<RoundTripBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripBranchState>> ExecuteAsync(RoundTripBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ValidateOrder));
        return Task.FromResult(StepResult<RoundTripBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Retail branch path: fulfils the order through the retail channel, then rejoins.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ProcessRetailOrder(WorkflowInvocationLog log) : IWorkflowStep<RoundTripBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripBranchState>> ExecuteAsync(RoundTripBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ProcessRetailOrder));
        return Task.FromResult(StepResult<RoundTripBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Default branch path: fulfils the order through the wholesale channel, then rejoins.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ProcessWholesaleOrder(WorkflowInvocationLog log) : IWorkflowStep<RoundTripBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripBranchState>> ExecuteAsync(RoundTripBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ProcessWholesaleOrder));
        return Task.FromResult(StepResult<RoundTripBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Declared terminal step: both branch paths rejoin here and the workflow completes.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ShipOrder(WorkflowInvocationLog log) : IWorkflowStep<RoundTripBranchState>
{
    /// <inheritdoc />
    public Task<StepResult<RoundTripBranchState>> ExecuteAsync(RoundTripBranchState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ShipOrder));
        return Task.FromResult(StepResult<RoundTripBranchState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// A branch workflow whose cases both rejoin the main flow at a declared terminal. Drives the
/// generator to emit <c>RoundtripBranchSaga</c>, <c>StartRoundtripBranchCommand</c> and
/// <c>AddRoundtripBranchWorkflow()</c>.
/// </summary>
[Workflow("roundtrip-branch")]
public static partial class RoundtripBranchWorkflowDefinition
{
    /// <summary>Gets the fluent definition: validate, route by channel, then ship.</summary>
    public static WorkflowDefinition<RoundTripBranchState> Definition => Workflow<RoundTripBranchState>
        .Create("roundtrip-branch")
        .StartWith<ValidateOrder>()
        .Branch(
            state => state.Channel,
            BranchCase<RoundTripBranchState, string>.When(
                RoundTripBranchChannels.Retail,
                path => path.Then<ProcessRetailOrder>()),
            BranchCase<RoundTripBranchState, string>.Otherwise(
                path => path.Then<ProcessWholesaleOrder>()))
        .Finally<ShipOrder>();
}

/// <summary>The sales channels the branch discriminates on.</summary>
public static class RoundTripBranchChannels
{
    /// <summary>The channel selecting the explicitly declared retail case.</summary>
    public const string Retail = "Retail";

    /// <summary>A channel with no declared case, so the default (otherwise) path is taken.</summary>
    public const string Wholesale = "Wholesale";
}
