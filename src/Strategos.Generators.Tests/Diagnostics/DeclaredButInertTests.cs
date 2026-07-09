// -----------------------------------------------------------------------
// <copyright file="DeclaredButInertTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// #143, G-6 6.2 — AGWF022 "declared-but-inert" diagnostic. A step configuration
/// member that is parsed into the <c>StepModel</c> IR but that no emitter consumes for
/// the step's kind is silently dropped today. AGWF022 surfaces that drop at compile time
/// so a deferred/unlowered configuration cannot masquerade as working.
/// </summary>
/// <remarks>
/// <para>
/// The concrete inert case this guards (verified against the generated saga): confidence
/// gating (<c>RequireConfidence</c>/<c>OnLowConfidence</c>) declared on an INTERMEDIATE
/// (non-last) step of a <c>Fork</c> path. The fork-path parse threads the configure lambda
/// into the IR — so an out-of-range threshold still surfaces AGWF018 — but the saga emitter
/// only lowers confidence-gated routing for a fork path's LAST step (DR-4 / #145 gap A, the
/// fork path-completed handler); an intermediate fork-path step runs through the generic
/// completed handler with no gate. That variant is deferred to v2.10.0 / DR-17 (#134), so
/// the configuration is inert: no <c>confidenceScore</c> gate and no <c>OnLowConfidence</c>
/// routing reach the generated saga.
/// </para>
/// <para>
/// Confidence on a fork path's LAST step is NOT flagged: it is now lowered, proven
/// behaviorally by <c>ForkPathConfidenceTests</c>.
/// </para>
/// <para>
/// AGWF022 is the next monotonic id past the live ceiling AGWF021 (INV-5: never reuse,
/// never renumber).
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public sealed class DeclaredButInertTests
{
    private const string DeclaredButInertId = "AGWF022";

    /// <summary>
    /// Verifies that confidence gating declared on an INTERMEDIATE (non-last) fork-path
    /// step — a configuration the generator still does not lower — fires AGWF022 at the
    /// workflow attribute call site. Only a fork path's LAST step is lowered (DR-4 / #145
    /// gap A); an intermediate one remains inert.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_StepConfigFieldInertForStepKind_ReportsAgwf022()
    {
        var source = ForkWorkflowWithIntermediatePathConfig(
            intermediateStepConfig: "step => step"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())");

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DeclaredButInertId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("confidence gating on an intermediate fork-path step is inert and must surface as AGWF022");
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("ForkedAssess");
    }

    /// <summary>
    /// Conformant-negative: confidence gating declared on a fork path's LAST step (which
    /// IS lowered into the fork path-completed handler — DR-4 / #145 gap A) must NOT fire
    /// AGWF022. This is the flip: before the lowering it fired; now it does not.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ForkPathLastStepConfidenceLowered_DoesNotReportAgwf022()
    {
        var source = ForkWorkflowWithPathConfig(
            forkPathStepConfig: "step => step"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())");

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
        var source = TopLevelWorkflowWithStepConfig(
            stepConfig: "step => step"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())");

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("confidence gating on a top-level step IS lowered, so AGWF022 must not fire");
    }

    /// <summary>
    /// Conformant-negative: a fork-path step that declares only LOWERED config (retry) and
    /// no confidence gating must NOT fire AGWF022 (the diagnostic is scoped to the inert
    /// config, not to all fork-path steps).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_ForkPathStepWithoutInertConfig_DoesNotReportAgwf022()
    {
        var source = ForkWorkflowWithPathConfig(
            forkPathStepConfig: "step => step.WithRetry(2)");

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("a fork-path step with only lowered (retry) config must not fire AGWF022");
    }

    /// <summary>
    /// Verifies that confidence gating declared on an INTERMEDIATE (non-last) loop-body step —
    /// a configuration the generator still does not lower for that position — fires AGWF022 at
    /// the workflow attribute call site. Task 009 promoted the loop body to configured
    /// <c>StepModel</c> records on <c>LoopModel.BodySteps</c>, so the config is now IN the IR
    /// (previously it was dropped entirely and structurally undiagnosable — #145 gap B); this
    /// diagnostic can now see it. Only a loop body's LAST step is lowered (DR-5 / #145 gap B).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_LoopBodyIntermediateStepConfig_ReportsAgwf022()
    {
        var source = LoopWorkflowWithIntermediateBodyConfig(
            intermediateStepConfig: "step => step"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())");

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DeclaredButInertId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("confidence gating on an intermediate loop-body step is inert and must surface as AGWF022");
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("CritiqueStep");
    }

    /// <summary>
    /// Conformant-negative: confidence gating declared on a loop body's LAST step (which IS
    /// lowered into the loop completed handler — DR-5 / #145 gap B) must NOT fire AGWF022. This
    /// is the flip that mirrors the fork path's last step: task 009 made the config visible in
    /// the IR and this task lowers it, so the diagnostic must not fire.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_LoopBodyLastStepConfidenceLowered_DoesNotReportAgwf022()
    {
        var source = LoopWorkflowWithLastBodyStepConfig(
            lastStepConfig: "step => step"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())");

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == DeclaredButInertId)).IsFalse()
            .Because("confidence gating on a loop body's last step IS lowered, so AGWF022 must not fire");
    }

    // =========================================================================
    // Source builder helpers
    // =========================================================================

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
