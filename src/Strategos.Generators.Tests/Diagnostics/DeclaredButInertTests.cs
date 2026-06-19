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
/// gating (<c>RequireConfidence</c>/<c>OnLowConfidence</c>) declared on a step that lives
/// on a <c>Fork</c> path. The fork-path parse threads the configure lambda into the IR —
/// so an out-of-range threshold still surfaces AGWF018 — but the saga emitter does not
/// lower confidence-gated routing for fork-path steps. That variant is deferred to
/// v2.10.0 / DR-17 (#134), so the configuration is inert: no <c>confidenceScore</c> gate
/// and no <c>OnLowConfidence</c> routing reach the generated saga.
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
    /// Verifies that confidence gating declared on a fork-path step — a configuration the
    /// generator does not lower for fork-path steps — fires AGWF022 at the workflow
    /// attribute call site.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Generator_StepConfigFieldInertForStepKind_ReportsAgwf022()
    {
        var source = ForkWorkflowWithPathConfig(
            forkPathStepConfig: "step => step"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())");

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == DeclaredButInertId);
        await Assert.That(diagnostic).IsNotNull()
            .Because("confidence gating on a fork-path step is inert and must surface as AGWF022");
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Warning);
        await Assert.That(diagnostic.GetMessage()).Contains("ForkedAssess");
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
}
