// -----------------------------------------------------------------------
// <copyright file="ResilienceDiagnosticsTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Diagnostics;

/// <summary>
/// DR-8 (epic #135, INV-5) — compile-time diagnostics for invalid step-resilience
/// configuration, surfaced from <c>WorkflowIncrementalGenerator</c> at the earliest
/// (emitter) tier. Each fires a stable next-free <c>AGWF0xx</c> id (≥ AGWF017) so
/// consumers can suppress it; the pre-existing builder-runtime throws
/// (<c>RequireConfidence</c> ∉ [0,1], retry <c>&lt; 1</c>) are mirrored as ids here.
/// </summary>
/// <remarks>
/// The diagnostic codes asserted below are the next-free ids verified against the
/// live AGWF ceiling (AGWF016) at GREEN time:
/// <list type="bullet">
///   <item>AGWF017 — <c>Compensate&lt;T&gt;</c> where T is not a registered step.</item>
///   <item>AGWF018 — <c>RequireConfidence(x)</c> with x outside [0.0, 1.0].</item>
///   <item>AGWF019 — <c>RequireConfidence</c> without a corresponding <c>OnLowConfidence</c>.</item>
///   <item>AGWF020 — retry <c>maxAttempts &lt; 1</c>.</item>
///   <item>AGWF021 — non-positive <c>WithTimeout</c>.</item>
/// </list>
/// </remarks>
[Property("Category", "Integration")]
public sealed class ResilienceDiagnosticsTests
{
    private const string CompensateNonStepId = "AGWF017";
    private const string ConfidenceOutOfRangeId = "AGWF018";
    private const string ConfidenceWithoutHandlerId = "AGWF019";
    private const string RetryBelowOneId = "AGWF020";
    private const string NonPositiveTimeoutId = "AGWF021";

    // =========================================================================
    // A. Compensate<T> where T is not a registered IWorkflowStep<TState> step.
    // =========================================================================

    /// <summary>
    /// Verifies that <c>Compensate&lt;T&gt;</c> where <c>T</c> does not implement
    /// <c>IWorkflowStep&lt;TState&gt;</c> fires the next-free AGWF id (AGWF017).
    /// </summary>
    [Test]
    public async Task Analyze_CompensateNonStepType_FiresNextFreeAgwf()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.Compensate<NotAStep>()",
            extraTypes: "public class NotAStep { }");

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == CompensateNonStepId);
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that <c>Compensate&lt;T&gt;</c> with a genuine step type does NOT
    /// fire AGWF017 (conformant-negative).
    /// </summary>
    [Test]
    public async Task Analyze_CompensateRegisteredStep_DoesNotFire()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.Compensate<RollbackStep>()",
            extraTypes: StepClass("RollbackStep"));

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == CompensateNonStepId)).IsFalse();
    }

    // =========================================================================
    // B. RequireConfidence(x) with x outside [0.0, 1.0].
    // =========================================================================

    /// <summary>
    /// Verifies that <c>RequireConfidence</c> with a threshold above 1.0 fires AGWF018.
    /// </summary>
    [Test]
    public async Task Analyze_RequireConfidenceOutOfRange_Fires()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.RequireConfidence(1.5).OnLowConfidence(alt => alt.Then<HumanReview>())",
            extraTypes: StepClass("HumanReview"));

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == ConfidenceOutOfRangeId);
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that <c>RequireConfidence</c> with a negative threshold fires AGWF018.
    /// </summary>
    [Test]
    public async Task Analyze_RequireConfidenceNegative_Fires()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.RequireConfidence(-0.1).OnLowConfidence(alt => alt.Then<HumanReview>())",
            extraTypes: StepClass("HumanReview"));

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == ConfidenceOutOfRangeId)).IsTrue();
    }

    /// <summary>
    /// Verifies that an in-range <c>RequireConfidence</c> does NOT fire AGWF018
    /// (conformant-negative — boundary 1.0 is valid).
    /// </summary>
    [Test]
    public async Task Analyze_RequireConfidenceInRange_DoesNotFire()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.RequireConfidence(1.0).OnLowConfidence(alt => alt.Then<HumanReview>())",
            extraTypes: StepClass("HumanReview"));

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == ConfidenceOutOfRangeId)).IsFalse();
    }

    // =========================================================================
    // C. RequireConfidence without a corresponding OnLowConfidence handler.
    // =========================================================================

    /// <summary>
    /// Verifies that <c>RequireConfidence</c> declared without a corresponding
    /// <c>OnLowConfidence</c> handler fires AGWF019.
    /// </summary>
    [Test]
    public async Task Analyze_RequireConfidenceWithoutOnLowConfidence_Fires()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.RequireConfidence(0.85)",
            extraTypes: string.Empty);

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == ConfidenceWithoutHandlerId);
        await Assert.That(diagnostic).IsNotNull();
    }

    /// <summary>
    /// Verifies that <c>RequireConfidence</c> WITH an <c>OnLowConfidence</c> handler
    /// does NOT fire AGWF019 (conformant-negative).
    /// </summary>
    [Test]
    public async Task Analyze_RequireConfidenceWithOnLowConfidence_DoesNotFire()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.RequireConfidence(0.85).OnLowConfidence(alt => alt.Then<HumanReview>())",
            extraTypes: StepClass("HumanReview"));

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == ConfidenceWithoutHandlerId)).IsFalse();
    }

    // =========================================================================
    // D. Retry maxAttempts < 1.
    // =========================================================================

    /// <summary>
    /// Verifies that a retry policy with <c>maxAttempts &lt; 1</c> fires AGWF020.
    /// </summary>
    [Test]
    public async Task Analyze_RetryMaxAttemptsBelowOne_Fires()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.WithRetry(0)",
            extraTypes: string.Empty);

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == RetryBelowOneId);
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// CodeRabbit F2 (PR #137): a unary-minus retry literal (<c>WithRetry(-1)</c>) must reach
    /// the IR so AGWF020 fires. The retry parser previously accepted only a direct numeric
    /// literal, so a negative literal never parsed and the diagnostic silently vanished —
    /// violating INV-5 (invalid config must surface, not disappear).
    /// </summary>
    [Test]
    public async Task Analyze_RetryMaxAttemptsNegativeLiteral_Fires()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.WithRetry(-1)",
            extraTypes: string.Empty);

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == RetryBelowOneId);
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that a valid retry count does NOT fire AGWF020 (conformant-negative).
    /// </summary>
    [Test]
    public async Task Analyze_RetryMaxAttemptsValid_DoesNotFire()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.WithRetry(3)",
            extraTypes: string.Empty);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == RetryBelowOneId)).IsFalse();
    }

    // =========================================================================
    // E. Non-positive WithTimeout.
    // =========================================================================

    /// <summary>
    /// Verifies that a non-positive timeout (<c>TimeSpan.Zero</c>) fires AGWF021.
    /// </summary>
    [Test]
    public async Task Analyze_NonPositiveTimeout_Fires()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.WithTimeout(TimeSpan.Zero)",
            extraTypes: string.Empty);

        var result = GeneratorTestHelper.RunGenerator(source);

        var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Id == NonPositiveTimeoutId);
        await Assert.That(diagnostic).IsNotNull();
        await Assert.That(diagnostic!.Severity).IsEqualTo(DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Verifies that a positive timeout does NOT fire AGWF021 (conformant-negative).
    /// </summary>
    [Test]
    public async Task Analyze_PositiveTimeout_DoesNotFire()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step.WithTimeout(TimeSpan.FromSeconds(30))",
            extraTypes: string.Empty);

        var result = GeneratorTestHelper.RunGenerator(source);

        await Assert.That(result.Diagnostics.Any(d => d.Id == NonPositiveTimeoutId)).IsFalse();
    }

    // =========================================================================
    // F. Fully conformant workflow — none of the resilience diagnostics fire.
    // =========================================================================

    /// <summary>
    /// Verifies that a fully conformant resilience configuration fires none of the
    /// AGWF017–AGWF021 resilience diagnostics.
    /// </summary>
    [Test]
    public async Task Analyze_FullyConformantResilience_FiresNoResilienceDiagnostics()
    {
        var source = WorkflowWithStepConfig(
            stepConfig: "step => step"
                + ".WithRetry(3, TimeSpan.FromSeconds(5))"
                + ".WithTimeout(TimeSpan.FromMinutes(2))"
                + ".RequireConfidence(0.85)"
                + ".OnLowConfidence(alt => alt.Then<HumanReview>())"
                + ".Compensate<RollbackStep>()",
            extraTypes: StepClass("HumanReview") + "\n" + StepClass("RollbackStep"));

        var result = GeneratorTestHelper.RunGenerator(source);

        var ids = new[]
        {
            CompensateNonStepId,
            ConfidenceOutOfRangeId,
            ConfidenceWithoutHandlerId,
            RetryBelowOneId,
            NonPositiveTimeoutId,
        };

        var fired = result.Diagnostics.Where(d => ids.Contains(d.Id)).Select(d => d.Id).ToList();
        await Assert.That(fired).IsEmpty();
    }

    // =========================================================================
    // Source builder helpers
    // =========================================================================

    private static string StepClass(string name) => $$"""
        public class {{name}} : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }
        """;

    /// <summary>
    /// Builds a single-step workflow whose <c>AssessClaim</c> step carries the
    /// supplied resilience <paramref name="stepConfig"/> configure lambda, plus any
    /// <paramref name="extraTypes"/> the config references.
    /// </summary>
    private static string WorkflowWithStepConfig(string stepConfig, string extraTypes) => $$"""
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

        public class SettleClaim : IWorkflowStep<ClaimState>
        {
            public Task<StepResult<ClaimState>> ExecuteAsync(
                ClaimState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<ClaimState>.FromState(state));
        }

        {{extraTypes}}

        [Workflow("resilience-claim")]
        public static partial class ResilienceClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("resilience-claim")
                .StartWith<IntakeClaim>()
                .Then<AssessClaim>({{stepConfig}})
                .Finally<SettleClaim>();
        }
        """;
}
