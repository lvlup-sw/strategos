// -----------------------------------------------------------------------
// <copyright file="DeclaredButInertTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// AGWF022 "declared-but-inert": a step configuration member that reaches the
/// <c>StepModel</c> IR but that no emitter consumes for that step's kind, so it is silently
/// dropped. The diagnostic surfaces the drop at compile time (INV-5, the earliest tier that
/// can catch it) so an un-lowered configuration cannot masquerade as working.
/// </summary>
/// <remarks>
/// <para>
/// The concrete inert case, verified against the generated saga: confidence gating
/// (<c>RequireConfidence</c>/<c>OnLowConfidence</c>) declared on the LAST step of a
/// non-terminal <c>Branch</c> case. The branch parse threads the configure lambda into the IR
/// — so an out-of-range threshold still surfaces the threshold-range code — but a non-terminal
/// case's last step is intercepted by the branch path-end handler, which routes straight to
/// the case's rejoin target and never reads the step's confidence. Neither the
/// <c>confidenceScore</c> comparison nor the <c>OnLowConfidence</c> routing reaches the saga.
/// </para>
/// <para>
/// The diagnostic previously pointed at confidence on an INTERMEDIATE fork-path or loop-body
/// step. Both were false positives: an intermediate path step falls through to the generic
/// completed handler, which gates on the step's confidence with no position test, so those
/// configurations DO lower. The negative tests below pin that by asserting the emitted gate,
/// not merely the absent diagnostic (#145).
/// </para>
/// <para>
/// The id is retargeted, never renumbered or reused (INV-5).
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class DeclaredButInertTests
{
    private const string DeclaredButInertId = "AGWF022";

    private const string ConfidenceGate = "step => step"
        + ".RequireConfidence(0.85)"
        + ".OnLowConfidence(alt => alt.Then<HumanReview>())";

    private const string LoweredRetryConfig = "step => step.WithRetry(2)";

    /// <summary>
    /// Confidence gating on the LAST step of a non-terminal branch case fires AGWF022.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_BranchCaseLastStepConfidence_ReportsAgwf022()
    {
        var source = BranchWorkflow(
            lastCaseStepConfig: ConfidenceGate,
            intermediateCaseStepConfig: LoweredRetryConfig);

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DeclaredButInertId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("confidence gating on a branch case's last step is inert and must surface as AGWF022");
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("PriceRepair");
    }

    /// <summary>
    /// The claim AGWF022 makes about that shape is true: the generated saga carries no
    /// confidence comparison at all for the branch case's last step.
    /// </summary>
    /// <remarks>
    /// Without this the diagnostic is unfalsifiable — a diagnostic that fires proves only that
    /// it fires, which is how the previous two emission sites survived while describing a
    /// lowering that had in fact landed.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_BranchCaseLastStepConfidence_LowersNoConfidenceGate()
    {
        var source = BranchWorkflow(
            lastCaseStepConfig: ConfidenceGate,
            intermediateCaseStepConfig: LoweredRetryConfig);

        var saga = GeneratorTestHelper.GetGeneratedSource(GeneratorTestHelper.RunGenerator(source), "Saga.g.cs");

        await Assert.That(saga)
            .DoesNotContain("confidenceScore")
            .Because("the branch path-end handler never compares the completed event's confidence");
    }

    /// <summary>
    /// Confidence gating on an INTERMEDIATE branch-case step does NOT fire — that step falls
    /// through to the generic completed handler, which gates.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_IntermediateBranchCaseStep_DoesNotFire()
    {
        var source = BranchWorkflow(
            lastCaseStepConfig: LoweredRetryConfig,
            intermediateCaseStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(saga)
            .Contains("confidenceScore < 0.85")
            .Because("an intermediate branch-case step's gate lowers into the generic completed handler");
        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a configuration that lowers must not be reported as inert");
    }

    /// <summary>
    /// A TERMINAL (<c>.Complete()</c>) case's last step does NOT fire: terminal cases are
    /// excluded from the branch path-end dispatch, so that step also reaches the gating
    /// generic completed handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_TerminalBranchCaseLastStep_DoesNotFire()
    {
        var source = TerminalBranchWorkflow(terminalCaseStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(saga)
            .Contains("confidenceScore < 0.85")
            .Because("a terminal case's last step is not intercepted, so its gate lowers");
        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a configuration that lowers must not be reported as inert");
    }

    /// <summary>
    /// Confidence gating on an INTERMEDIATE (non-last) FORK-PATH step lowers into the generic
    /// completed handler, so AGWF022 must not fire. This was one of the two false positives.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_IntermediateForkPathStep_DoesNotFire()
    {
        var source = ForkWorkflowWithIntermediatePathConfig(intermediateStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(saga)
            .Contains("confidenceScore < 0.85")
            .Because("the intermediate fork-path step's gate reaches the generated saga");
        await Assert.That(saga)
            .Contains("StartHumanReviewCommand")
            .Because("the OnLowConfidence handler chain is routed to from that gate");
        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("intermediate fork-path confidence lowers, so reporting it inert is a false positive");
    }

    /// <summary>
    /// Confidence gating on an INTERMEDIATE (non-last) LOOP-BODY step lowers into the generic
    /// completed handler, so AGWF022 must not fire. This was the other false positive.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_IntermediateLoopBodyStep_DoesNotFire()
    {
        var source = LoopWorkflowWithIntermediateBodyConfig(intermediateStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(saga)
            .Contains("confidenceScore < 0.85")
            .Because("the intermediate loop-body step's gate reaches the generated saga");
        await Assert.That(saga)
            .Contains("StartHumanReviewCommand")
            .Because("the OnLowConfidence handler chain is routed to from that gate");
        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("intermediate loop-body confidence lowers, so reporting it inert is a false positive");
    }

    /// <summary>
    /// Conformant-negative: confidence gating on a fork path's LAST step is lowered into the
    /// fork path-completed handler, so AGWF022 must not fire.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ForkPathLastStepConfidenceLowered_DoesNotReportAgwf022()
    {
        var source = ForkWorkflowWithPathConfig(forkPathStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("confidence gating on a fork path's last step IS lowered, so AGWF022 must not fire");
    }

    /// <summary>
    /// Conformant-negative: confidence gating declared on a TOP-LEVEL step (where it IS
    /// lowered into the saga) must NOT fire AGWF022.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_StepConfigFieldLoweredForStepKind_DoesNotReportAgwf022()
    {
        var source = TopLevelWorkflowWithStepConfig(stepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("confidence gating on a top-level step IS lowered, so AGWF022 must not fire");
    }

    /// <summary>
    /// Conformant-negative: a branch-case last step that declares only LOWERED config (retry)
    /// and no confidence gating must NOT fire AGWF022 — the diagnostic is scoped to the inert
    /// config, not to every branch-case step.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_BranchCaseStepWithoutInertConfig_DoesNotReportAgwf022()
    {
        var source = BranchWorkflow(
            lastCaseStepConfig: LoweredRetryConfig,
            intermediateCaseStepConfig: LoweredRetryConfig);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a branch-case step with only lowered (retry) config must not fire AGWF022");
    }

    /// <summary>
    /// Conformant-negative: a fork-path step that declares only LOWERED config (retry) and no
    /// confidence gating must NOT fire AGWF022.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ForkPathStepWithoutInertConfig_DoesNotReportAgwf022()
    {
        var source = ForkWorkflowWithPathConfig(forkPathStepConfig: LoweredRetryConfig);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a fork-path step with only lowered (retry) config must not fire AGWF022");
    }

    /// <summary>
    /// Conformant-negative: confidence gating declared on a loop body's LAST step (lowered
    /// into the loop completed handler) must NOT fire AGWF022.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_LoopBodyLastStepConfidenceLowered_DoesNotReportAgwf022()
    {
        var source = LoopWorkflowWithLastBodyStepConfig(lastStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("confidence gating on a loop body's last step IS lowered, so AGWF022 must not fire");
    }

    // =========================================================================
    // Source builder helpers
    // =========================================================================

    /// <summary>
    /// Builds a claim workflow whose first <c>Branch</c> case has TWO steps: an intermediate
    /// <c>AssessDamage</c> and a terminating <c>PriceRepair</c>, each carrying the supplied
    /// configure lambda. The case rejoins the main flow at <c>SettleClaim</c>, so it is
    /// non-terminal and its last step is intercepted by the branch path-end handler.
    /// </summary>
    /// <param name="lastCaseStepConfig">The configure lambda for the case's LAST step.</param>
    /// <param name="intermediateCaseStepConfig">The configure lambda for the case's INTERMEDIATE step.</param>
    /// <returns>The workflow source text.</returns>
    private static string BranchWorkflow(string lastCaseStepConfig, string intermediateCaseStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimKind { Collision, Liability }

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimKind Kind { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AssessDamage : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.WithConfidence(state, 0.5));
        }

        public class PriceRepair : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.WithConfidence(state, 0.5));
        }

        public class AssessLiability : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("inert-branch-claim")]
        public static partial class InertBranchClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("inert-branch-claim")
                .StartWith<IntakeClaim>()
                .Branch(state => state.Kind,
                    BranchCase<ClaimState, ClaimKind>.When(ClaimKind.Collision, path => path
                        .Then<AssessDamage>({{intermediateCaseStepConfig}})
                        .Then<PriceRepair>({{lastCaseStepConfig}})),
                    BranchCase<ClaimState, ClaimKind>.Otherwise(path => path.Then<AssessLiability>()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// Builds the same claim workflow with the first case marked TERMINAL via
    /// <c>Complete()</c>, so its last step is excluded from the branch path-end dispatch.
    /// </summary>
    /// <param name="terminalCaseStepConfig">The configure lambda for the terminal case's last step.</param>
    /// <returns>The workflow source text.</returns>
    private static string TerminalBranchWorkflow(string terminalCaseStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public enum ClaimKind { Collision, Liability }

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimKind Kind { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class PriceRepair : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.WithConfidence(state, 0.5));
        }

        public class AssessLiability : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("terminal-branch-claim")]
        public static partial class TerminalBranchClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("terminal-branch-claim")
                .StartWith<IntakeClaim>()
                .Branch(state => state.Kind,
                    BranchCase<ClaimState, ClaimKind>.When(ClaimKind.Collision, path =>
                    {
                        path.Then<PriceRepair>({{terminalCaseStepConfig}});
                        path.Complete();
                    }),
                    BranchCase<ClaimState, ClaimKind>.Otherwise(path => path.Then<AssessLiability>()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// Builds a workflow whose <c>ForkedAssess</c> step lives on the first <c>Fork</c> path
    /// and carries the supplied configure lambda. The second path carries a deterministic
    /// step so the fork is well-formed, and the fork is closed with a <c>Join</c>.
    /// </summary>
    private static string ForkWorkflowWithPathConfig(string forkPathStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ForkedAssess : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ForkedReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AggregateClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("inert-fork-claim")]
        public static partial class InertForkClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("inert-fork-claim")
                .StartWith<IntakeClaim>()
                .Fork(
                    path => path.Then<ForkedAssess>({{forkPathStepConfig}}),
                    path => path.Then<ForkedReview>())
                .Join<AggregateClaim>()
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// Builds a workflow whose first <c>Fork</c> path has TWO steps: an INTERMEDIATE
    /// <c>ForkedAssess</c> step carrying the supplied configure lambda followed by a
    /// terminating <c>ForkedFollowup</c> step, so the configured step is NOT the path's
    /// last step. A fork path's LAST step is lowered (DR-4), but an intermediate one is
    /// not — this exercises the still-inert surface AGWF022 guards.
    /// </summary>
    private static string ForkWorkflowWithIntermediatePathConfig(string intermediateStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ForkedAssess : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ForkedFollowup : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class ForkedReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AggregateClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("inert-fork-claim-intermediate")]
        public static partial class InertForkClaimIntermediateWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("inert-fork-claim-intermediate")
                .StartWith<IntakeClaim>()
                .Fork(
                    path => path.Then<ForkedAssess>({{intermediateStepConfig}}).Then<ForkedFollowup>(),
                    path => path.Then<ForkedReview>())
                .Join<AggregateClaim>()
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// Builds a top-level (non-fork) workflow whose <c>AssessClaim</c> step carries the
    /// supplied configure lambda — the conformant-lowered baseline.
    /// </summary>
    private static string TopLevelWorkflowWithStepConfig(string stepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class AssessClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("lowered-claim")]
        public static partial class LoweredClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("lowered-claim")
                .StartWith<IntakeClaim>()
                .Then<AssessClaim>({{stepConfig}})
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// Builds a workflow whose <c>RepeatUntil</c> loop body has TWO steps: an INTERMEDIATE
    /// <c>CritiqueStep</c> carrying the supplied configure lambda followed by a terminating
    /// <c>RefineStep</c>, so the configured step is NOT the loop body's last step. A loop
    /// body's LAST step is lowered (DR-5 / #145 gap B), but an intermediate one is not — this
    /// exercises the still-inert surface AGWF022 guards.
    /// </summary>
    private static string LoopWorkflowWithIntermediateBodyConfig(string intermediateStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record RefinementState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
        }

        public class ValidateInput : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class CritiqueStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.WithConfidence(state, 0.5));
        }

        public class RefineStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class PublishResult : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        [Workflow("inert-loop-refinement")]
        public static partial class InertLoopRefinementWorkflow
        {
            public static WorkflowDefinition<RefinementState> Definition => Workflow<RefinementState>
                .Create("inert-loop-refinement")
                .StartWith<ValidateInput>()
                .RepeatUntil(
                    state => state.QualityScore >= 0.9m,
                    "Refinement",
                    loop => loop
                        .Then<CritiqueStep>({{intermediateStepConfig}})
                        .Then<RefineStep>(),
                    maxIterations: 5)
                .Finally<PublishResult>();
        }
        """;

    /// <summary>
    /// Builds a workflow whose <c>RepeatUntil</c> loop body has a SINGLE step,
    /// <c>CritiqueStep</c> (both first and LAST), carrying the supplied configure lambda — the
    /// conformant-lowered baseline for loops (DR-5 / #145 gap B, lowered into the loop completed
    /// handler).
    /// </summary>
    private static string LoopWorkflowWithLastBodyStepConfig(string lastStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record RefinementState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
        }

        public class ValidateInput : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class CritiqueStep : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.WithConfidence(state, 0.5));
        }

        public class HumanReview : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class PublishResult : IWorkflowStep<RefinementState>
        {
            public Task<StepResult<RefinementState>> ExecuteAsync(
                RefinementState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        [Workflow("lowered-loop-refinement")]
        public static partial class LoweredLoopRefinementWorkflow
        {
            public static WorkflowDefinition<RefinementState> Definition => Workflow<RefinementState>
                .Create("lowered-loop-refinement")
                .StartWith<ValidateInput>()
                .RepeatUntil(
                    state => state.QualityScore >= 0.9m,
                    "Refinement",
                    loop => loop
                        .Then<CritiqueStep>({{lastStepConfig}}),
                    maxIterations: 5)
                .Finally<PublishResult>();
        }
        """;
}
