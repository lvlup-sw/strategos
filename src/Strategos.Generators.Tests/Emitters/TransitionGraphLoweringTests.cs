// -----------------------------------------------------------------------
// <copyright file="TransitionGraphLoweringTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Verifies that the generated <c>ValidTransitions</c> table describes a workflow's real phase
/// graph — fork dispatch and join, branch dispatch, case rejoin and case termination, loop
/// continue and exit — rather than a flat chain over the step-name list.
/// </summary>
/// <remarks>
/// <para>
/// These run the generator over the fluent DSL rather than hand-building a workflow model. The
/// defect being pinned is a disagreement between the step-name list and the constructs that
/// populate it, so a hand-built model would have to reproduce the extractors' own list ordering
/// to prove anything — and would silently stop proving it the moment that ordering moved.
/// </para>
/// <para>
/// Each expectation quotes a whole dictionary entry, closing bracket included, so it pins the
/// complete successor set of a phase and not merely the presence of one edge.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class TransitionGraphLoweringTests
{
    /// <summary>
    /// A fork whose first path step is confidence-gated, so the lowered handler step is appended
    /// to the step-name list AFTER the workflow's terminal step. Chaining consecutive list entries
    /// therefore publishes an edge out of the terminal and into the fork path's handler.
    /// </summary>
    private const string ForkWithGatedPathWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ShipmentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ReceiveShipment : IWorkflowStep<ShipmentState>
        {
            public Task<StepResult<ShipmentState>> ExecuteAsync(
                ShipmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ShipmentState>.FromState(state));
        }

        public class InspectCargo : IWorkflowStep<ShipmentState>
        {
            public Task<StepResult<ShipmentState>> ExecuteAsync(
                ShipmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ShipmentState>.WithConfidence(state, 0.5));
        }

        public class ReinspectCargo : IWorkflowStep<ShipmentState>
        {
            public Task<StepResult<ShipmentState>> ExecuteAsync(
                ShipmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ShipmentState>.FromState(state));
        }

        public class WeighCargo : IWorkflowStep<ShipmentState>
        {
            public Task<StepResult<ShipmentState>> ExecuteAsync(
                ShipmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ShipmentState>.FromState(state));
        }

        public class ConsolidateManifest : IWorkflowStep<ShipmentState>
        {
            public Task<StepResult<ShipmentState>> ExecuteAsync(
                ShipmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ShipmentState>.FromState(state));
        }

        public class DispatchShipment : IWorkflowStep<ShipmentState>
        {
            public Task<StepResult<ShipmentState>> ExecuteAsync(
                ShipmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ShipmentState>.FromState(state));
        }

        [Workflow("shipment-intake")]
        public static partial class ShipmentIntakeWorkflow
        {
            public static WorkflowDefinition<ShipmentState> Definition => Workflow<ShipmentState>
                .Create("shipment-intake")
                .StartWith<ReceiveShipment>()
                .Fork(
                    path => path.Then<InspectCargo>(step => step
                        .RequireConfidence(0.85)
                        .OnLowConfidence(alt => alt.Then<ReinspectCargo>())),
                    path => path.Then<WeighCargo>())
                .Join<ConsolidateManifest>()
                .Finally<DispatchShipment>();
        }
        """;

    /// <summary>
    /// An approval whose rejection chain and escalation chain each run two steps and each declare
    /// their own completion, so both are appended to the step-name list after the workflow's
    /// terminal and both must end in the completed phase rather than only in failure.
    /// </summary>
    private const string ApprovalWithTerminalChainsWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PayoutState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class AssessPayout : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class ReleaseFunds : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class ArchivePayout : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class RecordRefusal : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class NotifyBeneficiary : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class OpenDispute : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class AssignAdjudicator : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class PayoutOfficerApprover { }

        [Workflow("review-payout")]
        public static partial class ReviewPayoutWorkflow
        {
            public static WorkflowDefinition<PayoutState> Definition => Workflow<PayoutState>
                .Create("review-payout")
                .StartWith<AssessPayout>()
                .AwaitApproval<PayoutOfficerApprover>(approval => approval
                    .OnRejection(rejection => rejection
                        .Then<RecordRefusal>()
                        .Then<NotifyBeneficiary>()
                        .Complete())
                    .OnTimeout(escalation => escalation
                        .Then<OpenDispute>()
                        .Then<AssignAdjudicator>()
                        .Complete()))
                .Then<ReleaseFunds>()
                .Finally<ArchivePayout>();
        }
        """;

    /// <summary>
    /// The same approval with a rejection chain that never declares its own completion, so the
    /// chain rejoins the main flow instead of ending the workflow.
    /// </summary>
    private const string ApprovalWithResumingChainWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PayoutState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class AssessPayout : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class ReleaseFunds : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class ArchivePayout : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class RecordRefusal : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class NotifyBeneficiary : IWorkflowStep<PayoutState>
        {
            public Task<StepResult<PayoutState>> ExecuteAsync(
                PayoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PayoutState>.FromState(state));
        }

        public class PayoutOfficerApprover { }

        [Workflow("review-payout")]
        public static partial class ReviewPayoutWorkflow
        {
            public static WorkflowDefinition<PayoutState> Definition => Workflow<PayoutState>
                .Create("review-payout")
                .StartWith<AssessPayout>()
                .AwaitApproval<PayoutOfficerApprover>(approval => approval
                    .OnRejection(rejection => rejection
                        .Then<RecordRefusal>()
                        .Then<NotifyBeneficiary>()))
                .Then<ReleaseFunds>()
                .Finally<ArchivePayout>();
        }
        """;

    // =============================================================================
    // A. Fork Graph Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a fork workflow's terminal step completes the workflow and publishes no edge
    /// into a fork path, even when a path's lowered step is appended after it in the step-name list.
    /// </summary>
    [Test]
    public async Task ValidTransitions_ForkWorkflow_HasNoTerminalToPathEdge()
    {
        // Arrange & Act
        var source = EmitTransitions(ForkWithGatedPathWorkflow);

        // Assert - the terminal completes; it does not fall into the appended path handler
        await Assert.That(source).Contains(
            "ShipmentIntakePhase.DispatchShipment, [ShipmentIntakePhase.Completed, ShipmentIntakePhase.Failed]");
        await Assert.That(source).DoesNotContain(
            "ShipmentIntakePhase.DispatchShipment, [ShipmentIntakePhase.ReinspectCargo");

        // Assert - the fork's predecessor is the only phase that enters a path
        await Assert.That(CountEdgesInto(source, "ShipmentIntakePhase.InspectCargo")).IsEqualTo(1);
        await Assert.That(CountEdgesInto(source, "ShipmentIntakePhase.WeighCargo")).IsEqualTo(1);
        await Assert.That(source).Contains(
            "ShipmentIntakePhase.ReceiveShipment, [ShipmentIntakePhase.InspectCargo, ShipmentIntakePhase.WeighCargo, ShipmentIntakePhase.Failed]");
    }

    /// <summary>
    /// Verifies that a fork dispatches every path from its predecessor rather than entering only
    /// the first path.
    /// </summary>
    [Test]
    public async Task ValidTransitions_ForkWorkflow_DispatchesEveryPathFromThePredecessor()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithThreePathFork);

        // Assert
        await Assert.That(source).Contains(
            "MultiChannelNotificationPhase.PrepareNotification, ["
            + "MultiChannelNotificationPhase.SendEmail, "
            + "MultiChannelNotificationPhase.SendSms, "
            + "MultiChannelNotificationPhase.SendPush, "
            + "MultiChannelNotificationPhase.Failed]");
    }

    /// <summary>
    /// Verifies that parallel fork paths are not chained to one another: each path's last step
    /// reaches the join.
    /// </summary>
    [Test]
    public async Task ValidTransitions_ForkWorkflow_DoesNotChainSiblingPaths()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithFork);

        // Assert - each path reaches the join, not the next path
        await Assert.That(source).Contains(
            "ParallelOrderPhase.ProcessPayment, [ParallelOrderPhase.SynthesizeResults, ParallelOrderPhase.Failed]");
        await Assert.That(source).Contains(
            "ParallelOrderPhase.ReserveInventory, [ParallelOrderPhase.SynthesizeResults, ParallelOrderPhase.Failed]");
        await Assert.That(source).DoesNotContain(
            "ParallelOrderPhase.ProcessPayment, [ParallelOrderPhase.ReserveInventory");
    }

    // =============================================================================
    // B. Branch Graph Tests
    // =============================================================================

    /// <summary>
    /// Verifies that mutually exclusive branch cases are not chained to one another: every case's
    /// last step converges on the rejoin step.
    /// </summary>
    [Test]
    public async Task ValidTransitions_BranchWorkflow_DoesNotChainSiblingCases()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithEnumBranch);

        // Assert - each case converges on the rejoin step
        await Assert.That(source).Contains(
            "ProcessClaimPhase.ProcessAutoClaim, [ProcessClaimPhase.CompleteClaim, ProcessClaimPhase.Failed]");
        await Assert.That(source).Contains(
            "ProcessClaimPhase.ProcessHomeClaim, [ProcessClaimPhase.CompleteClaim, ProcessClaimPhase.Failed]");
        await Assert.That(source).Contains(
            "ProcessClaimPhase.ProcessLifeClaim, [ProcessClaimPhase.CompleteClaim, ProcessClaimPhase.Failed]");

        // Assert - no case runs into the next case
        await Assert.That(source).DoesNotContain(
            "ProcessClaimPhase.ProcessAutoClaim, [ProcessClaimPhase.ProcessHomeClaim");
        await Assert.That(source).DoesNotContain(
            "ProcessClaimPhase.ProcessHomeClaim, [ProcessClaimPhase.ProcessLifeClaim");

        // Assert - the discriminating step is the only phase that enters a case
        await Assert.That(CountEdgesInto(source, "ProcessClaimPhase.ProcessAutoClaim")).IsEqualTo(1);
        await Assert.That(CountEdgesInto(source, "ProcessClaimPhase.ProcessHomeClaim")).IsEqualTo(1);
        await Assert.That(CountEdgesInto(source, "ProcessClaimPhase.ProcessLifeClaim")).IsEqualTo(1);
    }

    /// <summary>
    /// Verifies that a branch dispatches every case from its discriminating step.
    /// </summary>
    [Test]
    public async Task ValidTransitions_BranchWorkflow_DispatchesEveryCaseFromThePredecessor()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithEnumBranch);

        // Assert
        await Assert.That(source).Contains(
            "ProcessClaimPhase.ValidateClaim, ["
            + "ProcessClaimPhase.ProcessAutoClaim, "
            + "ProcessClaimPhase.ProcessHomeClaim, "
            + "ProcessClaimPhase.ProcessLifeClaim, "
            + "ProcessClaimPhase.Failed]");
    }

    /// <summary>
    /// Verifies that a case which ends the workflow completes at its own last step instead of
    /// running on into the step that follows it in the step-name list.
    /// </summary>
    [Test]
    public async Task ValidTransitions_TerminalBranchCase_CompletesAtCaseEnd()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithTerminalBranch);

        // Assert - the terminal case completes
        await Assert.That(source).Contains(
            "ValidateOrderPhase.RejectOrder, [ValidateOrderPhase.Completed, ValidateOrderPhase.Failed]");
        await Assert.That(source).DoesNotContain(
            "ValidateOrderPhase.RejectOrder, [ValidateOrderPhase.ShipOrder");

        // Assert - the non-terminal sibling still rejoins
        await Assert.That(source).Contains(
            "ValidateOrderPhase.ProcessOrder, [ValidateOrderPhase.ShipOrder, ValidateOrderPhase.Failed]");
    }

    /// <summary>
    /// Verifies that a multi-step case chains within itself and rejoins only from its last step.
    /// </summary>
    [Test]
    public async Task ValidTransitions_MultiStepBranchCase_ChainsWithinCaseThenRejoins()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithMultiStepBranch);

        // Assert - interior of the long case
        await Assert.That(source).Contains(
            "ProcessTicketPhase.AssignAgent, [ProcessTicketPhase.EscalateToManager, ProcessTicketPhase.Failed]");
        await Assert.That(source).Contains(
            "ProcessTicketPhase.EscalateToManager, [ProcessTicketPhase.NotifyCustomer, ProcessTicketPhase.Failed]");

        // Assert - both cases rejoin from their own last step
        await Assert.That(source).Contains(
            "ProcessTicketPhase.NotifyCustomer, [ProcessTicketPhase.CloseTicket, ProcessTicketPhase.Failed]");
        await Assert.That(source).Contains(
            "ProcessTicketPhase.AddToQueue, [ProcessTicketPhase.CloseTicket, ProcessTicketPhase.Failed]");

        // Assert - the long case does not spill into the short one
        await Assert.That(source).DoesNotContain(
            "ProcessTicketPhase.NotifyCustomer, [ProcessTicketPhase.AddToQueue");
    }

    // =============================================================================
    // C. Loop Graph Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a loop body's last step publishes both the continue edge back to the body's
    /// first step and the exit edge to the continuation step.
    /// </summary>
    [Test]
    public async Task ValidTransitions_LoopWorkflow_LastBodyStepContinuesAndExits()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithLoop);

        // Assert
        await Assert.That(source).Contains(
            "IterativeRefinementPhase.Refinement_RefineStep, ["
            + "IterativeRefinementPhase.Refinement_CritiqueStep, "
            + "IterativeRefinementPhase.PublishResult, "
            + "IterativeRefinementPhase.Failed]");
    }

    /// <summary>
    /// Verifies that a loop which evaluates a branch on exit dispatches that branch's cases from
    /// the loop's last body step — the loop, not a step, is what precedes such a branch.
    /// </summary>
    [Test]
    public async Task ValidTransitions_LoopExitBranch_DispatchesCasesFromLastBodyStep()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.WorkflowWithLoopThenBranch);

        // Assert - continue plus one edge per exit case
        await Assert.That(source).Contains(
            "LoopThenBranchPhase.TaskLoop_ExecuteTaskStep, ["
            + "LoopThenBranchPhase.TaskLoop_SelectTaskStep, "
            + "LoopThenBranchPhase.CompleteStep, "
            + "LoopThenBranchPhase.EscalateStep, "
            + "LoopThenBranchPhase.FailedStep, "
            + "LoopThenBranchPhase.Failed]");

        // Assert - every exit case terminates the workflow, none chains to the next
        await Assert.That(source).Contains(
            "LoopThenBranchPhase.CompleteStep, [LoopThenBranchPhase.Completed, LoopThenBranchPhase.Failed]");
        await Assert.That(source).Contains(
            "LoopThenBranchPhase.EscalateStep, [LoopThenBranchPhase.Completed, LoopThenBranchPhase.Failed]");
        await Assert.That(source).DoesNotContain(
            "LoopThenBranchPhase.CompleteStep, [LoopThenBranchPhase.EscalateStep");
    }

    // =============================================================================
    // D. Approval Graph Tests
    // =============================================================================

    /// <summary>
    /// Verifies that a rejection or escalation chain which declared its own completion publishes
    /// the edge to the completed phase from its last step, and chains within itself before that.
    /// </summary>
    /// <remarks>
    /// A terminal approval path is not a terminal failure handler. A failure handler ends in
    /// <c>Failed</c>, an edge every step already carries, so it claims no forward edge; an approval
    /// path that declared its own completion ends in <c>Completed</c>, which nothing else supplies.
    /// Publishing only the failure edge left the table forbidding the very transition the generated
    /// saga performs at that step.
    /// </remarks>
    [Test]
    public async Task ValidTransitions_TerminalApprovalPath_CompletesAtChainEnd()
    {
        // Arrange & Act
        var source = EmitTransitions(ApprovalWithTerminalChainsWorkflow);

        // Assert - each chain's last step completes the workflow
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.NotifyBeneficiary, [ReviewPayoutPhase.Completed, ReviewPayoutPhase.Failed]");
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.AssignAdjudicator, [ReviewPayoutPhase.Completed, ReviewPayoutPhase.Failed]");

        // Assert - each chain chains within itself and never spills into its sibling
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.RecordRefusal, [ReviewPayoutPhase.NotifyBeneficiary, ReviewPayoutPhase.Failed]");
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.OpenDispute, [ReviewPayoutPhase.AssignAdjudicator, ReviewPayoutPhase.Failed]");
        await Assert.That(source).DoesNotContain(
            "ReviewPayoutPhase.NotifyBeneficiary, [ReviewPayoutPhase.OpenDispute");

        // Assert - the main flow is untouched by the appended chains
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.AssessPayout, [ReviewPayoutPhase.ReleaseFunds, ReviewPayoutPhase.Failed]");
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.ArchivePayout, [ReviewPayoutPhase.Completed, ReviewPayoutPhase.Failed]");
    }

    /// <summary>
    /// Verifies that a rejection chain which never declared its own completion rejoins the main
    /// flow from its last step, at the step an approved decision resumes onto.
    /// </summary>
    [Test]
    public async Task ValidTransitions_ResumingApprovalPath_RejoinsTheMainFlow()
    {
        // Arrange & Act
        var source = EmitTransitions(ApprovalWithResumingChainWorkflow);

        // Assert
        await Assert.That(source).Contains(
            "ReviewPayoutPhase.NotifyBeneficiary, [ReviewPayoutPhase.ReleaseFunds, ReviewPayoutPhase.Failed]");
        await Assert.That(source).DoesNotContain(
            "ReviewPayoutPhase.NotifyBeneficiary, [ReviewPayoutPhase.Completed");
    }

    // =============================================================================
    // E. Linear Workflow Regression
    // =============================================================================

    /// <summary>
    /// Verifies that a workflow with no fork, branch or loop still publishes the plain sequence.
    /// </summary>
    [Test]
    public async Task ValidTransitions_LinearWorkflow_StillChainsSequentially()
    {
        // Arrange & Act
        var source = EmitTransitions(SourceTexts.LinearWorkflow);

        // Assert
        await Assert.That(source).Contains(
            "ProcessOrderPhase.NotStarted, [ProcessOrderPhase.ValidateOrder]");
        await Assert.That(source).Contains(
            "ProcessOrderPhase.ValidateOrder, [ProcessOrderPhase.ProcessPayment, ProcessOrderPhase.Failed]");
        await Assert.That(source).Contains(
            "ProcessOrderPhase.SendConfirmation, [ProcessOrderPhase.Completed, ProcessOrderPhase.Failed]");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static string EmitTransitions(string workflowSource)
    {
        var result = GeneratorTestHelper.RunGenerator(workflowSource);
        return GeneratorTestHelper.GetGeneratedSource(result, "Transitions.g.cs");
    }

    /// <summary>
    /// Counts the dictionary entries whose successor array contains the given phase.
    /// </summary>
    /// <param name="source">The generated transitions source.</param>
    /// <param name="qualifiedPhaseName">The fully qualified phase name to look for as a target.</param>
    /// <returns>The number of phases that publish an edge into <paramref name="qualifiedPhaseName"/>.</returns>
    private static int CountEdgesInto(string source, string qualifiedPhaseName)
    {
        var count = 0;
        foreach (var line in source.Split('\n'))
        {
            var open = line.IndexOf('[', StringComparison.Ordinal);
            var close = line.LastIndexOf(']');
            if (open < 0 || close <= open)
            {
                continue;
            }

            var targets = line.Substring(open + 1, close - open - 1)
                .Split(',')
                .Select(t => t.Trim());

            if (targets.Contains(qualifiedPhaseName, StringComparer.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
