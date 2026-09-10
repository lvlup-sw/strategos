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
/// (<c>RequireConfidence</c>/<c>OnLowConfidence</c>) declared on the step an
/// <c>AwaitApproval</c> checkpoint follows. The configure lambda reaches the IR — so an
/// out-of-range threshold still surfaces the threshold-range code — and the handler chain is
/// even lowered into its own phase, start command and worker handler. But that step's completed
/// handler becomes the approval-request handler, which moves the saga into the waiting phase and
/// asks for the decision, so the <c>confidenceScore</c> comparison is never emitted and the
/// declared chain is unreachable.
/// </para>
/// <para>
/// Two earlier targets were retired as their gaps closed, and the negative tests below hold each
/// closure down by asserting the emitted gate rather than merely the absent diagnostic (#145).
/// An INTERMEDIATE fork-path or loop-body step was a false positive from the start: it falls
/// through to the generic completed handler, which gates with no position test. A
/// <c>Branch</c> case's LAST step was genuinely inert while the path-end handler had no
/// confidence handling of its own; it has that handling now, for a rejoining and a
/// workflow-ending case alike, so the gate lowers there too.
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

    private const string UnderwriterConfidenceGate = "step => step"
        + ".RequireConfidence(0.85)"
        + ".OnLowConfidence(alt => alt.Then<EscalateToUnderwriter>())";

    private const string LoweredRetryConfig = "step => step.WithRetry(2)";

    /// <summary>
    /// Confidence gating on the step an approval checkpoint follows fires AGWF022.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_ApprovalPrecedingStepConfidence_ReportsAgwf022()
    {
        var source = ApprovalWorkflow(precedingStepConfig: UnderwriterConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DeclaredButInertId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("confidence gating on the step an approval follows is inert and must surface as AGWF022");
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("ScoreApplicant");
    }

    /// <summary>
    /// The claim AGWF022 makes about that shape is true: the generated saga carries no
    /// confidence comparison at all for the step the approval follows.
    /// </summary>
    /// <remarks>
    /// Without this the diagnostic is unfalsifiable — a diagnostic that fires proves only that
    /// it fires, which is how earlier emission sites survived while describing a lowering that
    /// had in fact landed.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_ApprovalPrecedingStepConfidence_LowersNoConfidenceGate()
    {
        var source = ApprovalWorkflow(precedingStepConfig: UnderwriterConfidenceGate);

        var saga = GeneratorTestHelper.GetGeneratedSource(GeneratorTestHelper.RunGenerator(source), "Saga.g.cs");

        await Assert.That(saga)
            .DoesNotContain("confidenceScore")
            .Because("the approval-request handler replaces the gated completed handler outright");
        await Assert.That(saga)
            .Contains("StartEscalateToUnderwriterCommand")
            .Because("the handler chain IS lowered — that is what makes the drop invisible without the diagnostic");
    }

    /// <summary>
    /// An approval whose preceding step declares only LOWERED config (retry) must NOT fire —
    /// the diagnostic is scoped to the inert config, not to every approval checkpoint.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ApprovalPrecedingStepWithoutInertConfig_DoesNotReportAgwf022()
    {
        var source = ApprovalWorkflow(precedingStepConfig: LoweredRetryConfig);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a step with only lowered (retry) config must not fire AGWF022");
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
    /// A TERMINAL (<c>.Complete()</c>) case's last step does NOT fire: the branch path-end
    /// handler that intercepts it emits the confidence gate itself.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_TerminalBranchCaseLastStep_DoesNotFire()
    {
        var source = MixedBranchWorkflow(
            rejoiningCaseStepConfig: LoweredRetryConfig,
            endingCaseStepConfig: ConfidenceGate);

        var result = GeneratorTestHelper.RunGenerator(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(saga)
            .Contains("confidenceScore < 0.85")
            .Because("the path-end handler gates a workflow-ending case's last step");
        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a configuration that lowers must not be reported as inert");
    }

    /// <summary>
    /// A REJOINING case's last step does NOT fire either — the position AGWF022 used to report,
    /// which the path-end handler now gates.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeclaredButInert_RejoiningBranchCaseLastStep_DoesNotFire()
    {
        var source = MixedBranchWorkflow(
            rejoiningCaseStepConfig: ConfidenceGate,
            endingCaseStepConfig: LoweredRetryConfig);

        var result = GeneratorTestHelper.RunGenerator(source);
        var saga = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        await Assert.That(saga)
            .Contains("confidenceScore < 0.85")
            .Because("the path-end handler gates a rejoining case's last step");
        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a configuration that lowers must not be reported as inert");
    }

    /// <summary>
    /// The gate for a workflow-ending case's last step lands INSIDE that case's path-end
    /// handler, ahead of the completion, and routes below-threshold results to the declared
    /// handler instead of completing the saga.
    /// </summary>
    /// <remarks>
    /// Asserting only that the saga contains a gate somewhere would pass on a gate emitted for
    /// any other step, so both proofs delimit the handler first.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchCase_TerminalLastStepConfidence_LowersIntoPathEndHandler()
    {
        var handler = BranchCaseHandlerFor(
            MixedBranchWorkflow(
                rejoiningCaseStepConfig: LoweredRetryConfig,
                endingCaseStepConfig: ConfidenceGate),
            "AssessLiabilityCompleted");

        await Assert.That(handler).Contains("if (evt.Confidence is double confidenceScore && confidenceScore < 0.85)")
            .Because("the ending case's last step declared a threshold, and this handler is the only one that sees its event");
        await Assert.That(handler).Contains("yield return new StartHumanReviewCommand(WorkflowId);")
            .Because("a below-threshold score must reach the declared handler chain");
        await Assert.That(handler.IndexOf("confidenceScore < 0.85", StringComparison.Ordinal))
            .IsLessThan(handler.IndexOf("MarkCompleted();", StringComparison.Ordinal))
            .Because("a gate evaluated after the saga is marked completed would never route anything");
    }

    /// <summary>
    /// The same gate lands inside a REJOINING case's path-end handler, ahead of the rejoin.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchCase_RejoiningLastStepConfidence_LowersIntoPathEndHandler()
    {
        var handler = BranchCaseHandlerFor(
            MixedBranchWorkflow(
                rejoiningCaseStepConfig: ConfidenceGate,
                endingCaseStepConfig: LoweredRetryConfig),
            "AssessDamageCompleted");

        await Assert.That(handler).Contains("if (evt.Confidence is double confidenceScore && confidenceScore < 0.85)")
            .Because("the rejoining case's last step declared a threshold, and this handler is the only one that sees its event");
        await Assert.That(handler).Contains("yield return new StartHumanReviewCommand(WorkflowId);")
            .Because("a below-threshold score must reach the declared handler chain");
        await Assert.That(handler.IndexOf("confidenceScore < 0.85", StringComparison.Ordinal))
            .IsLessThan(handler.IndexOf("StartSettleClaimCommand", StringComparison.Ordinal))
            .Because("a gate evaluated after the rejoin command is emitted would never divert the path");
    }

    /// <summary>
    /// The branch's routing switch dispatches each case to the case's OWN first step.
    /// </summary>
    /// <remarks>
    /// A raw descendant walk of the case lambda also collected the <c>Then&lt;THandler&gt;</c>
    /// written inside <c>OnLowConfidence</c>, and that nested call sorted ahead of the step
    /// owning it — so the branch dispatched straight to the handler and the case's own steps
    /// never ran. Every gate proof above would still pass under that defect, because the gate is
    /// emitted into a handler nothing reaches.
    /// </remarks>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BranchRouting_CaseWithConfidenceGatedStep_DispatchesToTheCaseStep()
    {
        var source = MixedBranchWorkflow(
            rejoiningCaseStepConfig: ConfidenceGate,
            endingCaseStepConfig: LoweredRetryConfig);

        var saga = GeneratorTestHelper.GetGeneratedSource(GeneratorTestHelper.RunGenerator(source), "Saga.g.cs");

        await Assert.That(saga)
            .Contains("ClaimKind.Collision => new StartAssessDamageCommand(WorkflowId),")
            .Because("the case's first step is the step it declared, not the handler named inside its OnLowConfidence lambda");
        await Assert.That(saga)
            .DoesNotContain("=> new StartHumanReviewCommand(WorkflowId),")
            .Because("the low-confidence handler is reached from the gate, never from the branch's routing switch");
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
    /// Builds a claim workflow whose branch mixes a REJOINING case (<c>AssessDamage</c>, falling
    /// through to <c>SettleClaim</c>) with a WORKFLOW-ENDING case (<c>AssessLiability</c>,
    /// declaring <c>Complete()</c>), ahead of a declared terminal. Each case's single step carries
    /// the supplied configure lambda.
    /// </summary>
    /// <remarks>
    /// The mixed shape is the discriminating one: the rejoining case gives the branch a
    /// convergence point, so a path-end handler that reads only the branch-level flag would treat
    /// both cases alike. The discriminator is an enum, not a <c>bool</c> (#179).
    /// </remarks>
    /// <param name="rejoiningCaseStepConfig">The configure lambda for the rejoining case's step.</param>
    /// <param name="endingCaseStepConfig">The configure lambda for the workflow-ending case's step.</param>
    /// <returns>The workflow source text.</returns>
    private static string MixedBranchWorkflow(
        string rejoiningCaseStepConfig,
        string endingCaseStepConfig) => $$"""
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

        public sealed record ClaimState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public ClaimKind Kind { get; init; }
        }

        public sealed class IntakeClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class AssessDamage : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.WithConfidence(state, 0.5));
        }

        public sealed class AssessLiability : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.WithConfidence(state, 0.5));
        }

        public sealed class HumanReview : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        public sealed class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        [Workflow("mixed-branch-claim")]
        public static partial class MixedBranchClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("mixed-branch-claim")
                .StartWith<IntakeClaim>()
                .Branch(state => state.Kind,
                    BranchCase<ClaimState, ClaimKind>.When(ClaimKind.Collision, path => path
                        .Then<AssessDamage>({{rejoiningCaseStepConfig}})),
                    BranchCase<ClaimState, ClaimKind>.Otherwise(path => path
                        .Then<AssessLiability>({{endingCaseStepConfig}})
                        .Complete()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// Builds a loan workflow whose <c>ScoreApplicant</c> step carries the supplied configure
    /// lambda and is immediately followed by an <c>AwaitApproval</c> checkpoint.
    /// </summary>
    /// <param name="precedingStepConfig">The configure lambda for the step the approval follows.</param>
    /// <returns>The workflow source text.</returns>
    private static string ApprovalWorkflow(string precedingStepConfig) => $$"""
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public sealed record LoanState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public sealed class ReceiveApplication : IWorkflowStep<LoanState>
        {
            public Task<StepResult<LoanState>> ExecuteAsync(
                LoanState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanState>.FromState(state));
        }

        public sealed class ScoreApplicant : IWorkflowStep<LoanState>
        {
            public Task<StepResult<LoanState>> ExecuteAsync(
                LoanState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanState>.WithConfidence(state, 0.5));
        }

        public sealed class EscalateToUnderwriter : IWorkflowStep<LoanState>
        {
            public Task<StepResult<LoanState>> ExecuteAsync(
                LoanState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanState>.FromState(state));
        }

        public sealed class NotifyApplicantDeclined : IWorkflowStep<LoanState>
        {
            public Task<StepResult<LoanState>> ExecuteAsync(
                LoanState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanState>.FromState(state));
        }

        public sealed class IssueLoan : IWorkflowStep<LoanState>
        {
            public Task<StepResult<LoanState>> ExecuteAsync(
                LoanState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<LoanState>.FromState(state));
        }

        public sealed class LoanOfficerApprover
        {
        }

        [Workflow("loan-approval")]
        public static partial class LoanApprovalWorkflow
        {
            public static WorkflowDefinition<LoanState> Definition => Workflow<LoanState>
                .Create("loan-approval")
                .StartWith<ReceiveApplication>()
                .Then<ScoreApplicant>({{precedingStepConfig}})
                .AwaitApproval<LoanOfficerApprover>(approval => approval
                    .WithOption("approve", "Approve", "Grant the loan.", isDefault: true)
                    .WithOption("decline", "Decline", "Refuse the loan.")
                    .OnRejection(rejection => rejection
                        .Then<NotifyApplicantDeclined>()
                        .Complete()))
                .Finally<IssueLoan>();
        }
        """;

    /// <summary>
    /// Extracts the single completed-event handler method for the named event from the saga the
    /// supplied source generates, signature through closing brace.
    /// </summary>
    /// <param name="source">The workflow source text to run the generator over.</param>
    /// <param name="eventName">The completed-event type name the handler accepts.</param>
    /// <returns>The handler method's source text.</returns>
    private static string BranchCaseHandlerFor(string source, string eventName)
    {
        var saga = GeneratorTestHelper.GetGeneratedSource(
            GeneratorTestHelper.RunGenerator(source), "Saga.g.cs");

        var parameter = $"{eventName} evt,";
        var parameterIndex = saga.IndexOf(parameter, StringComparison.Ordinal);
        if (parameterIndex < 0)
        {
            throw new InvalidOperationException(
                $"The emitted saga has no handler accepting '{eventName}'. Emitted source:{Environment.NewLine}{saga}");
        }

        var start = saga.LastIndexOf("    public ", parameterIndex, StringComparison.Ordinal);
        var end = saga.IndexOf($"{Environment.NewLine}    }}", parameterIndex, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException(
                $"Could not delimit the handler for '{eventName}'. Emitted source:{Environment.NewLine}{saga}");
        }

        return saga.Substring(start, end - start);
    }

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
