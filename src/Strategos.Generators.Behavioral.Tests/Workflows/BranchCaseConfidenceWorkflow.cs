// -----------------------------------------------------------------------
// <copyright file="BranchCaseConfidenceWorkflow.cs" company="Levelup Software">
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
// A claims workflow whose branch carries a confidence gate on the LAST step of
// EACH case — one case rejoining, one ending the workflow:
//
//     ScreenClaim
//       ├── Route == Repair    → AssessRepairCost  ──→ SettleClaim (terminal)
//       │                             ↓ below 0.85
//       │                        EscalateRepairEstimate (ends here)
//       └── otherwise          → AssessTotalLoss .Complete()
//                                     ↓ below 0.85
//                                EscalateTotalLoss (ends here)
//
// A case's last step is intercepted by the branch path-end handler, which is the
// only handler that sees its completed event. Unless that handler compares the
// event's confidence itself, the declared threshold and its OnLowConfidence chain
// are dropped: the chain is still lowered into its own phase, start command and
// worker handler, so the drop is invisible from the generated surface — the
// escalation step simply never runs.
//
// Both scores come from state so the same shape drives a below-threshold run and
// an at-threshold one; the pair is what makes the gate discriminating, since an
// emitter that always cascaded to the handler would satisfy the low run alone.
// The discriminator is an enum, not a bool (#179).
// =============================================================================

/// <summary>How a screened claim is routed, which selects the branch case.</summary>
public enum ClaimRoute
{
    /// <summary>The claim is a total loss and is settled on its own path.</summary>
    TotalLoss = 0,

    /// <summary>The claim is repairable and rejoins the main flow to be settled.</summary>
    Repair = 1,
}

/// <summary>State for the branch-case confidence workflow: a claim and its assessment score.</summary>
[WorkflowState]
public sealed record BranchCaseConfidenceState : IWorkflowState
{
    /// <summary>Gets the workflow instance identity.</summary>
    public Guid WorkflowId { get; init; }

    /// <summary>Gets the route that selects the branch case.</summary>
    public ClaimRoute Route { get; init; }

    /// <summary>
    /// Gets the confidence the assessing step reports. Defaults to full confidence so a run that
    /// does not set it exercises the ordinary, ungated path.
    /// </summary>
    public double AssessmentConfidence { get; init; } = 1.0;

    /// <summary>Gets the number of steps that folded their result into state.</summary>
    public int StepCount { get; init; }
}

/// <summary>Pre-branch step: screens the claim and decides which case applies.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class ScreenClaim(WorkflowInvocationLog log) : IWorkflowStep<BranchCaseConfidenceState>
{
    /// <inheritdoc />
    public Task<StepResult<BranchCaseConfidenceState>> ExecuteAsync(BranchCaseConfidenceState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(ScreenClaim));
        return Task.FromResult(StepResult<BranchCaseConfidenceState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// The REJOINING case's last step: reports the repair estimate's confidence, and on a
/// sufficient score rejoins the main flow at the declared terminal.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class AssessRepairCost(WorkflowInvocationLog log) : IWorkflowStep<BranchCaseConfidenceState>
{
    /// <inheritdoc />
    public Task<StepResult<BranchCaseConfidenceState>> ExecuteAsync(BranchCaseConfidenceState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(AssessRepairCost));
        return Task.FromResult(StepResult<BranchCaseConfidenceState>.WithConfidence(
            state with { StepCount = state.StepCount + 1 },
            state.AssessmentConfidence));
    }
}

/// <summary>
/// The WORKFLOW-ENDING case's last step: reports the total-loss valuation's confidence, and on a
/// sufficient score ends the workflow at this step rather than at the declared terminal.
/// </summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class AssessTotalLoss(WorkflowInvocationLog log) : IWorkflowStep<BranchCaseConfidenceState>
{
    /// <inheritdoc />
    public Task<StepResult<BranchCaseConfidenceState>> ExecuteAsync(BranchCaseConfidenceState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(AssessTotalLoss));
        return Task.FromResult(StepResult<BranchCaseConfidenceState>.WithConfidence(
            state with { StepCount = state.StepCount + 1 },
            state.AssessmentConfidence));
    }
}

/// <summary>The rejoining case's low-confidence handler: a human re-estimates the repair.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EscalateRepairEstimate(WorkflowInvocationLog log) : IWorkflowStep<BranchCaseConfidenceState>
{
    /// <inheritdoc />
    public Task<StepResult<BranchCaseConfidenceState>> ExecuteAsync(BranchCaseConfidenceState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(EscalateRepairEstimate));
        return Task.FromResult(StepResult<BranchCaseConfidenceState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>The ending case's low-confidence handler: a human reviews the total-loss call.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class EscalateTotalLoss(WorkflowInvocationLog log) : IWorkflowStep<BranchCaseConfidenceState>
{
    /// <inheritdoc />
    public Task<StepResult<BranchCaseConfidenceState>> ExecuteAsync(BranchCaseConfidenceState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(EscalateTotalLoss));
        return Task.FromResult(StepResult<BranchCaseConfidenceState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>Declared terminal step, reachable only from the rejoining case's accepted estimate.</summary>
/// <param name="log">The shared invocation log injected by the host.</param>
public sealed class SettleClaim(WorkflowInvocationLog log) : IWorkflowStep<BranchCaseConfidenceState>
{
    /// <inheritdoc />
    public Task<StepResult<BranchCaseConfidenceState>> ExecuteAsync(BranchCaseConfidenceState state, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));

        log.Record(nameof(SettleClaim));
        return Task.FromResult(StepResult<BranchCaseConfidenceState>.FromState(state with { StepCount = state.StepCount + 1 }));
    }
}

/// <summary>
/// A branch workflow whose rejoining and workflow-ending cases each carry a confidence gate on
/// their last step. Drives the generator to emit <c>BranchCaseConfidenceSaga</c>,
/// <c>StartBranchCaseConfidenceCommand</c> and <c>AddBranchCaseConfidenceWorkflow()</c>.
/// </summary>
[Workflow("branch-case-confidence")]
public static partial class BranchCaseConfidenceWorkflowDefinition
{
    /// <summary>Gets the fluent definition: screen the claim, route it, and gate each case's assessment.</summary>
    public static WorkflowDefinition<BranchCaseConfidenceState> Definition => Workflow<BranchCaseConfidenceState>
        .Create("branch-case-confidence")
        .StartWith<ScreenClaim>()
        .Branch(
            state => state.Route,
            BranchCase<BranchCaseConfidenceState, ClaimRoute>.When(
                ClaimRoute.Repair,
                path => path.Then<AssessRepairCost>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<EscalateRepairEstimate>()))),
            BranchCase<BranchCaseConfidenceState, ClaimRoute>.Otherwise(
                path => path.Then<AssessTotalLoss>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<EscalateTotalLoss>()))
                    .Complete()))
        .Finally<SettleClaim>();
}
