// -----------------------------------------------------------------------
// <copyright file="StepExtractorContextTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// TDD Cycle 1: Tests for StepContext tracking in StepExtractor.
/// These tests verify that steps are properly marked with their context
/// (Linear, ForkPath, BranchPath) to enable context-aware duplicate detection.
/// </summary>
[Property("Category", "Unit")]
public class StepExtractorContextTests
{
    // =============================================================================
    // A. Linear Flow Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos returns all steps with Linear context
    /// for a simple linear workflow.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_LinearFlow_AllStepsHaveLinearContext()
    {
        // Arrange
        var context = CreateContext(SourceTexts.LinearWorkflow);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert
        await Assert.That(rawSteps.Count).IsEqualTo(3);
        await Assert.That(rawSteps.All(s => s.Context == StepContext.Linear)).IsTrue();
    }

    // =============================================================================
    // B. Fork Path Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos marks fork path steps with ForkPath context.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_ForkPaths_StepsHaveForkPathContext()
    {
        // Arrange - WorkflowWithFork has fork paths with ProcessPayment and ReserveInventory
        var context = CreateContext(SourceTexts.WorkflowWithFork);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - fork path steps should have ForkPath context
        var forkSteps = rawSteps.Where(s => s.StepName is "ProcessPayment" or "ReserveInventory").ToList();
        await Assert.That(forkSteps.Count).IsEqualTo(2);
        await Assert.That(forkSteps.All(s => s.Context == StepContext.ForkPath)).IsTrue();
    }

    /// <summary>
    /// Verifies that non-fork steps in a fork workflow still have Linear context.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_ForkWorkflow_NonForkStepsHaveLinearContext()
    {
        // Arrange
        var context = CreateContext(SourceTexts.WorkflowWithFork);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - ValidateOrder and SendConfirmation should be Linear
        var linearSteps = rawSteps.Where(s => s.StepName is "ValidateOrder" or "SendConfirmation").ToList();
        await Assert.That(linearSteps.Count).IsEqualTo(2);
        await Assert.That(linearSteps.All(s => s.Context == StepContext.Linear)).IsTrue();
    }

    // =============================================================================
    // C. Branch Path Context Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos marks branch path steps with BranchPath context.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_BranchPaths_StepsHaveBranchPathContext()
    {
        // Arrange - WorkflowWithEnumBranch has branch paths with ProcessAutoClaim, ProcessHomeClaim, ProcessLifeClaim
        var context = CreateContext(SourceTexts.WorkflowWithEnumBranch);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - branch path steps should have BranchPath context
        var branchSteps = rawSteps.Where(s =>
            s.StepName is "ProcessAutoClaim" or "ProcessHomeClaim" or "ProcessLifeClaim").ToList();

        await Assert.That(branchSteps.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(branchSteps.All(s => s.Context == StepContext.BranchPath)).IsTrue();
    }

    // =============================================================================
    // D. No Deduplication Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ExtractRawStepInfos does NOT deduplicate steps.
    /// This is the key difference from ExtractStepInfos - duplicates are preserved
    /// so that duplicate detection can work correctly.
    /// </summary>
    [Test]
    public async Task ExtractRawStepInfos_DuplicateStepsInForkPaths_PreservesDuplicates()
    {
        // Arrange - Create a workflow with duplicate steps in fork paths
        const string duplicateForkWorkflow = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record AnalysisState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            [Workflow("duplicate-fork")]
            public static partial class DuplicateForkWorkflow
            {
                public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                    .Create("duplicate-fork")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>(),
                        path => path.Then<AnalyzeStep>())
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(duplicateForkWorkflow);

        // Act
        var rawSteps = StepExtractor.ExtractRawStepInfos(context);

        // Assert - should have TWO AnalyzeStep entries (no deduplication)
        var analyzeStepCount = rawSteps.Count(s => s.StepName == "AnalyzeStep");
        await Assert.That(analyzeStepCount).IsEqualTo(2);
    }

    /// <summary>
    /// Verifies that the existing ExtractStepInfos still deduplicates (unchanged behavior).
    /// </summary>
    [Test]
    public async Task ExtractStepInfos_DuplicateStepsInForkPaths_Deduplicates()
    {
        // Arrange - Same workflow as above
        const string duplicateForkWorkflow = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record AnalysisState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class PrepareStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class AnalyzeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class SynthesizeStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            public class CompleteStep : IWorkflowStep<AnalysisState>
            {
                public Task<StepResult<AnalysisState>> ExecuteAsync(
                    AnalysisState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<AnalysisState>.FromState(state));
            }

            [Workflow("duplicate-fork")]
            public static partial class DuplicateForkWorkflow
            {
                public static WorkflowDefinition<AnalysisState> Definition => Workflow<AnalysisState>
                    .Create("duplicate-fork")
                    .StartWith<PrepareStep>()
                    .Fork(
                        path => path.Then<AnalyzeStep>(),
                        path => path.Then<AnalyzeStep>())
                    .Join<SynthesizeStep>()
                    .Finally<CompleteStep>();
            }
            """;

        var context = CreateContext(duplicateForkWorkflow);

        // Act
        var dedupedSteps = StepExtractor.ExtractStepInfos(context);

        // Assert - should have only ONE AnalyzeStep (deduplication preserved)
        var analyzeStepCount = dedupedSteps.Count(s => s.StepName == "AnalyzeStep");
        await Assert.That(analyzeStepCount).IsEqualTo(1);
    }

    // =============================================================================
    // E. Step-List Ordering Oracles
    // =============================================================================
    //
    // A workflow's steps have two in-generator representations, and nothing anywhere
    // asserts they agree as an ORDERED sequence:
    //
    //   * the phase-name list the saga emitter walks to pick each step's successor
    //     (ExtractStepInfos -> PhaseName, which is what FluentDslParser.ExtractStepNames
    //     returns and what the workflow model carries as StepNames), and
    //   * the step-model list every other consumer reads (ExtractStepModels, which the
    //     workflow model carries as Steps).
    //
    // Both are produced by walking the same fluent chain backwards. The step-model walker
    // splices each construct's path steps in as a block, so its output is document-ordered.
    // The phase-name walker instead delegates fork and branch paths to helpers that append
    // to the tail of a caller-owned list, so a top-level fork's or branch's path steps land
    // AFTER the terminal step. The forward (in-loop) walkers append in source order, so a
    // fork nested inside a repeat-until loop already agrees.
    //
    // These fixtures use only shapes with no failure handlers, approval points or
    // confidence handlers, so no post-parse append block contributes and the parse-tier
    // lists are exactly the two model lists.

    /// <summary>
    /// A workflow whose repeat-until loop body contains a fork and its join, exercising the
    /// FORWARD path-collection walkers rather than the backward top-level one.
    /// </summary>
    private const string ForkInsideRepeatUntilWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record FulfilmentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal AllocationScore { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class AllocateStock : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ChargePayment : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ReserveInventory : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ConfirmAllocation : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        public class ShipOrder : IWorkflowStep<FulfilmentState>
        {
            public Task<StepResult<FulfilmentState>> ExecuteAsync(
                FulfilmentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<FulfilmentState>.FromState(state));
        }

        [Workflow("fulfil-order")]
        public static partial class FulfilOrderWorkflow
        {
            public static WorkflowDefinition<FulfilmentState> Definition => Workflow<FulfilmentState>
                .Create("fulfil-order")
                .StartWith<ValidateOrder>()
                .RepeatUntil(
                    state => state.AllocationScore >= 0.9m,
                    "Fulfilment",
                    loop => loop
                        .Then<AllocateStock>()
                        .Fork(
                            path => path.Then<ChargePayment>(),
                            path => path.Then<ReserveInventory>())
                        .Join<ConfirmAllocation>(),
                    maxIterations: 5)
                .Finally<ShipOrder>();
        }
        """;

    /// <summary>
    /// For a top-level <c>Fork</c>, the phase-name list must equal the step-model list as an
    /// ORDERED sequence — both in document order, terminal last.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepNames_ForkWorkflow_MatchesDocumentOrder()
    {
        var context = CreateContext(SourceTexts.WorkflowWithFork);

        var phaseNames = PhaseNameList(context);
        var stepModelNames = StepModelPhaseNameList(context);

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe([
                "ValidateOrder", "ProcessPayment", "ReserveInventory", "SynthesizeResults", "SendConfirmation",
            ]))
            .Because("a top-level fork's path steps sit between the pre-fork step and the join in the source");

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe(stepModelNames))
            .Because(
                "the phase-name list the saga emitter walks must agree with the step-model list "
                + "as an ORDERED sequence, not merely as a set");
    }

    /// <summary>
    /// For a top-level <c>Branch</c>, the phase-name list must equal the step-model list as
    /// an ORDERED sequence — both in document order, terminal last.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepNames_BranchWorkflow_MatchesDocumentOrder()
    {
        var context = CreateContext(SourceTexts.WorkflowWithEnumBranch);

        var phaseNames = PhaseNameList(context);
        var stepModelNames = StepModelPhaseNameList(context);

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe([
                "ValidateClaim", "ProcessAutoClaim", "ProcessHomeClaim", "ProcessLifeClaim", "CompleteClaim",
            ]))
            .Because("a top-level branch's case steps sit between the discriminator step and the terminal in the source");

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe(stepModelNames))
            .Because(
                "the phase-name list the saga emitter walks must agree with the step-model list "
                + "as an ORDERED sequence, not merely as a set");
    }

    /// <summary>
    /// For a <c>Fork</c> nested inside a <c>RepeatUntil</c> the two representations ALREADY
    /// agree, because the in-loop walkers append path steps in source order.
    /// </summary>
    /// <remarks>
    /// This case is the stay-green discriminator for the splice-direction repair. Moving the
    /// splice decision to each caller — backward callers splicing at the front, forward
    /// callers appending — leaves this green. Flipping the shared path helpers to insert at
    /// the front in place would make the top-level cases pass while turning this one RED,
    /// because the in-loop caller would then get its path steps reversed into the wrong
    /// position. It is expected to pass both before and after that work; a run in which it
    /// changes state is a signal the repair was made in the wrong place.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StepNames_ForkInsideRepeatUntil_MatchesDocumentOrder()
    {
        var context = CreateContext(ForkInsideRepeatUntilWorkflow);

        var phaseNames = PhaseNameList(context);
        var stepModelNames = StepModelPhaseNameList(context);

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe([
                "ValidateOrder",
                "Fulfilment_AllocateStock",
                "Fulfilment_ChargePayment",
                "Fulfilment_ReserveInventory",
                "Fulfilment_ConfirmAllocation",
                "ShipOrder",
            ]))
            .Because("the in-loop walkers append path steps in source order, so this shape is already document-ordered");

        await Assert.That(Describe(phaseNames))
            .IsEqualTo(Describe(stepModelNames))
            .Because(
                "the two representations must agree as an ORDERED sequence for a fork nested in a loop");
    }

    // =============================================================================
    // F. Repeated-Phase-Name Collapse Oracles
    // =============================================================================
    //
    // A step run inside a branch case may also be run on the main flow — one phase, one
    // command, one handler, so the deduped list carries the name once. WHICH occurrence
    // survives is decided by first-occurrence order, and moving path steps from the tail of
    // the list into their document position moves the path occurrence in front of the linear
    // one for any workflow that declares the branch first. The surviving entry's execution
    // context therefore stopped being a property of the workflow and became a property of
    // where the author happened to put the branch.
    //
    // Two things are pinned here, separately, because they answer to different rules:
    //   * POSITION is the first occurrence's, which is what keeps the phase-name list
    //     index-aligned with the step-model list; and
    //   * CONTEXT is Linear whenever any occurrence is Linear, because a step that runs on
    //     the main flow is a main-flow step no matter what else also runs it.

    /// <summary>
    /// A workflow that runs <c>RecordDecision</c> inside a branch case AND on the main flow
    /// after the branch rejoins, with the branch declared FIRST — so the branch-path
    /// occurrence precedes the linear one in document order.
    /// </summary>
    private const string SharedStepDeclaredOnBranchCaseFirstWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimKind { Collision, Liability }

        public record SettlementState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimKind Kind { get; init; }
        }

        public class ValidateClaim : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        public class AssessLiability : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        public class RecordDecision : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        public class CloseClaim : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        [Workflow("settle-claim")]
        public static partial class SettleClaimWorkflow
        {
            public static WorkflowDefinition<SettlementState> Definition => Workflow<SettlementState>
                .Create("settle-claim")
                .StartWith<ValidateClaim>()
                .Branch(state => state.Kind,
                    BranchCase<SettlementState, ClaimKind>.When(ClaimKind.Collision, path => path.Then<RecordDecision>()),
                    BranchCase<SettlementState, ClaimKind>.Otherwise(path => path.Then<AssessLiability>()))
                .Then<RecordDecision>()
                .Finally<CloseClaim>();
        }
        """;

    /// <summary>
    /// A workflow that runs <c>NotifyAdjuster</c> on two exclusive branch cases and nowhere on
    /// the main flow — the control for the context rule.
    /// </summary>
    private const string SharedStepOnTwoBranchCasesWorkflow = """
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimKind { Collision, Liability }

        public record SettlementState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimKind Kind { get; init; }
        }

        public class ValidateClaim : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        public class NotifyAdjuster : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        public class CloseClaim : IWorkflowStep<SettlementState>
        {
            public Task<StepResult<SettlementState>> ExecuteAsync(
                SettlementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<SettlementState>.FromState(state));
        }

        [Workflow("settle-claim")]
        public static partial class SettleClaimWorkflow
        {
            public static WorkflowDefinition<SettlementState> Definition => Workflow<SettlementState>
                .Create("settle-claim")
                .StartWith<ValidateClaim>()
                .Branch(state => state.Kind,
                    BranchCase<SettlementState, ClaimKind>.When(ClaimKind.Collision, path => path.Then<NotifyAdjuster>()),
                    BranchCase<SettlementState, ClaimKind>.Otherwise(path => path.Then<NotifyAdjuster>()))
                .Finally<CloseClaim>();
        }
        """;

    /// <summary>
    /// Both occurrences of a shared name collapse to ONE entry, and that entry sits where the
    /// first occurrence sat — the branch case's slot, not the later main-flow slot.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractStepInfos_StepOnBranchCaseAndMainFlow_KeepsOneEntryAtFirstOccurrence()
    {
        var context = CreateContext(SharedStepDeclaredOnBranchCaseFirstWorkflow);

        var rawOccurrences = StepExtractor.ExtractRawStepInfos(context)
            .Count(s => s.PhaseName == "RecordDecision");
        var deduped = StepExtractor.ExtractStepInfos(context);

        await Assert.That(rawOccurrences)
            .IsEqualTo(2)
            .Because("the fixture must actually declare the name twice for the collapse to be exercised");

        await Assert.That(deduped.Count(s => s.PhaseName == "RecordDecision"))
            .IsEqualTo(1)
            .Because("a repeated phase name gets one phase, one command and one handler");

        await Assert.That(Describe(deduped.Select(s => s.PhaseName)))
            .IsEqualTo(Describe(["ValidateClaim", "RecordDecision", "AssessLiability", "CloseClaim"]))
            .Because("the surviving entry keeps the FIRST occurrence's position, which is inside the branch");

        await Assert.That(Describe(deduped.Select(s => s.PhaseName)))
            .IsEqualTo(Describe(StepModelPhaseNameList(context)))
            .Because(
                "first-occurrence position is what keeps the phase-name list index-aligned with "
                + "the step-model list; collapsing to a different occurrence desynchronises them");
    }

    /// <summary>
    /// The surviving entry reports <c>Linear</c> — the context of the main-flow occurrence —
    /// even though the branch-path occurrence is the one that came first.
    /// </summary>
    /// <remarks>
    /// This is the assertion the splice reorder put at risk. It must be decided by the rule
    /// "any Linear occurrence wins", not by whichever occurrence document order happens to put
    /// first: a step that runs on the main flow is a main-flow step regardless of a branch case
    /// also running it, and declaring the branch earlier in the source does not change that.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractStepInfos_StepOnBranchCaseAndMainFlow_SurvivingEntryReportsLinearContext()
    {
        var context = CreateContext(SharedStepDeclaredOnBranchCaseFirstWorkflow);

        var deduped = StepExtractor.ExtractStepInfos(context);
        var recordDecision = deduped.Single(s => s.PhaseName == "RecordDecision");

        await Assert.That(recordDecision.Context)
            .IsEqualTo(StepContext.Linear)
            .Because(
                "RecordDecision runs on the main flow after the branch rejoins, so it is a "
                + "main-flow step even though the branch case that also runs it is declared first");

        await Assert.That(deduped.Single(s => s.PhaseName == "AssessLiability").Context)
            .IsEqualTo(StepContext.BranchPath)
            .Because("a step that runs only inside a branch case is not promoted to the main flow");
    }

    /// <summary>
    /// A name shared by two exclusive branch cases and run nowhere on the main flow stays
    /// <c>BranchPath</c> — the collapse promotes a context, it does not overwrite one.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractStepInfos_StepOnTwoBranchCasesOnly_SurvivingEntryStaysBranchPath()
    {
        var context = CreateContext(SharedStepOnTwoBranchCasesWorkflow);

        var rawOccurrences = StepExtractor.ExtractRawStepInfos(context)
            .Count(s => s.PhaseName == "NotifyAdjuster");
        var deduped = StepExtractor.ExtractStepInfos(context);

        await Assert.That(rawOccurrences)
            .IsEqualTo(2)
            .Because("the fixture must actually declare the name on both cases");

        await Assert.That(deduped.Single(s => s.PhaseName == "NotifyAdjuster").Context)
            .IsEqualTo(StepContext.BranchPath)
            .Because("no occurrence is on the main flow, so there is no main-flow membership to preserve");
    }

    // =============================================================================
    // Private Helpers
    // =============================================================================

    /// <summary>
    /// The ordered phase-name list the saga emitter walks — what the workflow model carries
    /// as its step names, taken at the parse tier.
    /// </summary>
    /// <param name="context">The parse context for the workflow under test.</param>
    /// <returns>The ordered phase names.</returns>
    private static IReadOnlyList<string> PhaseNameList(FluentDslParseContext context) =>
        [.. StepExtractor.ExtractStepInfos(context).Select(s => s.PhaseName)];

    /// <summary>
    /// The ordered phase-name list of the step models — what the workflow model carries as
    /// its steps, taken at the parse tier.
    /// </summary>
    /// <param name="context">The parse context for the workflow under test.</param>
    /// <returns>The ordered phase names of the step models.</returns>
    private static IReadOnlyList<string> StepModelPhaseNameList(FluentDslParseContext context) =>
        [.. StepExtractor.ExtractStepModels(context).Select(s => s.PhaseName)];

    /// <summary>
    /// Renders a step list for an assertion message.
    /// </summary>
    /// <param name="steps">The step names to render.</param>
    /// <returns>A bracketed, comma-separated rendering.</returns>
    private static string Describe(IEnumerable<string> steps) => "[" + string.Join(", ", steps) + "]";

    private static FluentDslParseContext CreateContext(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        return FluentDslParseContext.Create(root, semanticModel, null, CancellationToken.None);
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();

        // Add core runtime references
        var runtimePath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var coreAssemblies = new[]
        {
            "System.Runtime.dll",
            "System.Private.CoreLib.dll",
            "netstandard.dll",
        };

        foreach (var assembly in coreAssemblies)
        {
            var path = Path.Combine(runtimePath, assembly);
            if (File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        // Add loaded assemblies (filtering out dynamic ones)
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                }
                catch
                {
                    // Ignore assemblies that can't be loaded as references
                }
            }
        }

        // Add the Workflow library reference
        var workflowAssembly = typeof(Strategos.Abstractions.IWorkflowState).Assembly;
        if (!string.IsNullOrEmpty(workflowAssembly.Location))
        {
            references.Add(MetadataReference.CreateFromFile(workflowAssembly.Location));
        }

        return references;
    }
}
