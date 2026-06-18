// -----------------------------------------------------------------------
// <copyright file="StepExtractorResilienceTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;
using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Helpers;

/// <summary>
/// Tests that <see cref="StepExtractor"/> parses the per-step resilience configuration
/// declared inside the <c>Then&lt;TStep&gt;(step =&gt; step.WithRetry(...).WithTimeout(...)...)</c>
/// configure lambda into the step's <see cref="StepModel"/> IR (epic #135, DR-1).
/// </summary>
/// <remarks>
/// These tests drive a workflow snippet through <see cref="ParserTestHelper.ExtractStepModels"/>,
/// which routes through <c>FluentDslParser.ExtractStepModels</c> →
/// <see cref="StepExtractor.ExtractStepModels"/>, returning the configured
/// <see cref="StepModel"/> list. The resilience models
/// (<see cref="RetryModel"/>, <see cref="TimeoutModel"/>, <see cref="CompensationModel"/>,
/// <see cref="ConfidenceModel"/>) are internal but visible to this test project.
/// </remarks>
[Property("Category", "Unit")]
public sealed class StepExtractorResilienceTests
{
    // =========================================================================
    // Task 002 — WithRetry / WithTimeout
    // =========================================================================

    /// <summary>
    /// Verifies that <c>.WithRetry(int, TimeSpan)</c> and <c>.WithTimeout(TimeSpan)</c>
    /// declared in a step's configure lambda populate the step's
    /// <see cref="RetryModel"/> and <see cref="TimeoutModel"/>.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_StepWithWithRetryAndTimeout_PopulatesRetryAndTimeoutModels()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceLinearWorkflow);

        // Act
        var processStep = stepModels.Single(s => s.StepName == "ProcessPayment");

        // Assert - retry policy
        await Assert.That(processStep.Retry).IsNotNull();
        await Assert.That(processStep.Retry!.MaxAttempts).IsEqualTo(3);
        await Assert.That(processStep.Retry!.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(5));

        // Assert - timeout policy
        await Assert.That(processStep.Timeout).IsNotNull();
        await Assert.That(processStep.Timeout!.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    /// <summary>
    /// Verifies that the single-argument <c>.WithRetry(int)</c> overload populates
    /// <see cref="RetryModel.MaxAttempts"/> with a null <see cref="RetryModel.InitialDelay"/>.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_StepWithWithRetryNoDelay_PopulatesMaxAttemptsOnly()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceLinearWorkflow);

        // Act
        var auditStep = stepModels.Single(s => s.StepName == "AuditPayment");

        // Assert
        await Assert.That(auditStep.Retry).IsNotNull();
        await Assert.That(auditStep.Retry!.MaxAttempts).IsEqualTo(2);
        await Assert.That(auditStep.Retry!.InitialDelay).IsNull();
        await Assert.That(auditStep.Timeout).IsNull();
    }

    /// <summary>
    /// CodeRabbit F3 (PR #137): an extreme timeout literal (<c>WithTimeout(TimeSpan.FromDays(1e18))</c>)
    /// must NOT crash the parser. <c>TimeSpan.From*</c> throws <c>OverflowException</c> on an
    /// out-of-range argument; the parser must catch that and return "not parsed" (no timeout) instead
    /// of letting the exception escape and abort generation.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_ExtremeTimeoutLiteral_DoesNotThrowAndDropsTimeout()
    {
        // Act — must not throw (the parser swallows the TimeSpan.From* overflow).
        var stepModels = ParserTestHelper.ExtractStepModels(ExtremeTimeoutWorkflow);

        // Assert — the step is still parsed; the out-of-range timeout is simply not carried.
        var step = stepModels.Single(s => s.StepName == "ProcessPayment");
        await Assert.That(step.Timeout).IsNull();
    }

    /// <summary>
    /// CodeRabbit F2 (PR #137): a unary-minus retry literal (<c>WithRetry(-1)</c>) must reach the
    /// IR as a negative <see cref="RetryModel.MaxAttempts"/>, so the downstream AGWF020 diagnostic
    /// can fire (INV-5). The retry parser previously accepted only a direct numeric literal, so the
    /// negative literal silently dropped — the whole <see cref="RetryModel"/> vanished.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_StepWithNegativeRetryLiteral_CarriesNegativeMaxAttempts()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(NegativeRetryWorkflow);

        // Act
        var step = stepModels.Single(s => s.StepName == "ProcessPayment");

        // Assert - the negative retry reaches the IR (not dropped)
        await Assert.That(step.Retry).IsNotNull();
        await Assert.That(step.Retry!.MaxAttempts).IsEqualTo(-1);
    }

    // =========================================================================
    // Task 003 — Compensate<T> / RequireConfidence / OnLowConfidence
    // =========================================================================

    /// <summary>
    /// Verifies that <c>.Compensate&lt;TCompensation&gt;()</c> carries the compensation
    /// step's identity as its fully qualified type name (INV-8: a string descriptor,
    /// never a CLR <see cref="System.Type"/>).
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_CompensateOfT_CarriesCompensationStepSymbolKey()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceCompensationConfidenceWorkflow);

        // Act
        var assessStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Assert
        await Assert.That(assessStep.Compensation).IsNotNull();
        await Assert.That(assessStep.Compensation!.CompensationStepTypeName)
            .IsEqualTo("TestNamespace.RollbackAssessment");
        await Assert.That(assessStep.Compensation!.RequiredOnFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that <c>.RequireConfidence(double)</c> + <c>.OnLowConfidence(alt =&gt; alt.Then&lt;T&gt;())</c>
    /// populate the step's <see cref="ConfidenceModel"/> with the threshold and the
    /// low-confidence handler's step identifier.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_RequireConfidenceOnLowConfidence_PopulatesConfidenceModel()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceCompensationConfidenceWorkflow);

        // Act
        var assessStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Assert
        await Assert.That(assessStep.Confidence).IsNotNull();
        await Assert.That(assessStep.Confidence!.Threshold).IsEqualTo(0.85);
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerId).IsEqualTo("HumanReview");

        // The single-step handler chain carries exactly that step and terminates
        // (no rejoin marker) by default (G-4 / #139).
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain).IsNotNull();
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain!.Steps.Count).IsEqualTo(1);
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain!.Steps[0].StepName).IsEqualTo("HumanReview");
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain!.RejoinsMainFlow).IsFalse();
    }

    /// <summary>
    /// G-4 / #139: a TWO-step <c>OnLowConfidence</c> chain
    /// (<c>OnLowConfidence(alt =&gt; alt.Then&lt;A&gt;().Then&lt;B&gt;())</c>) extracts BOTH handler
    /// steps as an ORDERED chain (A before B), with the first step retained as the
    /// confidence gate's routing target. Before #139 only one step was captured.
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_TwoStepOnLowConfidenceChain_ExtractsOrderedSteps()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ConfidenceChainWorkflow);

        // Act
        var assessStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Assert — the ordered handler chain carries both steps in declaration order.
        await Assert.That(assessStep.Confidence).IsNotNull();
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain).IsNotNull();
        var chain = assessStep.Confidence!.OnLowConfidenceHandlerChain!;
        await Assert.That(chain.Steps.Count).IsEqualTo(2);
        await Assert.That(chain.Steps[0].StepName).IsEqualTo("HumanReview");
        await Assert.That(chain.Steps[1].StepName).IsEqualTo("EscalateReview");

        // The gate's routing target (first handler) is retained for back-compat.
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerId).IsEqualTo("HumanReview");

        // No rejoin marker → terminating (the back-compat default).
        await Assert.That(chain.RejoinsMainFlow).IsFalse();
    }

    /// <summary>
    /// G-4 / #139: a <c>.RejoinMainFlow()</c> call inside the <c>OnLowConfidence</c>
    /// lambda is inferred as the chain's REJOIN marker
    /// (<c>RejoinsMainFlow == true</c>); its absence terminates (asserted above).
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_RejoinMainFlowMarker_SetsRejoinsMainFlow()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ConfidenceRejoinWorkflow);

        // Act
        var assessStep = stepModels.Single(s => s.StepName == "AssessClaim");

        // Assert
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain).IsNotNull();
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain!.RejoinsMainFlow).IsTrue();
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain!.Steps.Count).IsEqualTo(1);
        await Assert.That(assessStep.Confidence!.OnLowConfidenceHandlerChain!.Steps[0].StepName).IsEqualTo("HumanReview");
    }

    // =========================================================================
    // Task 004 — loop + fork-path parse parity
    // =========================================================================

    /// <summary>
    /// Verifies that resilience config declared on a step inside a <c>RepeatUntil</c>
    /// loop body is parsed identically to a top-level step (loop parse parity).
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_WithRetryInsideLoopStep_PopulatesRetryModel()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceLoopWorkflow);

        // Act - the loop body step is prefixed with its loop name
        var refineStep = stepModels.Single(s => s.StepName == "RefineDraft");

        // Assert
        await Assert.That(refineStep.LoopName).IsEqualTo("Refinement");
        await Assert.That(refineStep.Retry).IsNotNull();
        await Assert.That(refineStep.Retry!.MaxAttempts).IsEqualTo(4);
        await Assert.That(refineStep.Retry!.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(3));
        await Assert.That(refineStep.Timeout).IsNotNull();
        await Assert.That(refineStep.Timeout!.Timeout).IsEqualTo(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Verifies that resilience config declared on a step inside a <c>Fork</c> path
    /// is parsed identically to a top-level step (fork-path parse parity).
    /// </summary>
    [Test]
    public async Task WalkInvocationChain_WithRetryInsideForkPathStep_PopulatesRetryModel()
    {
        // Arrange
        var stepModels = ParserTestHelper.ExtractStepModels(ResilienceForkWorkflow);

        // Act
        var paymentStep = stepModels.Single(s => s.StepName == "ChargeCard");

        // Assert
        await Assert.That(paymentStep.Retry).IsNotNull();
        await Assert.That(paymentStep.Retry!.MaxAttempts).IsEqualTo(5);
        await Assert.That(paymentStep.Retry!.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(2));
        await Assert.That(paymentStep.Timeout).IsNotNull();
        await Assert.That(paymentStep.Timeout!.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    // =========================================================================
    // Test source workflows
    // =========================================================================

    /// <summary>
    /// A linear workflow whose steps declare retry/timeout via the configure lambda.
    /// </summary>
    private const string ResilienceLinearWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PaymentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidatePayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class AuditPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class SendReceipt : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        [Workflow("resilience-linear")]
        public static partial class ResilienceLinearWorkflow
        {
            public static WorkflowDefinition<PaymentState> Definition => Workflow<PaymentState>
                .Create("resilience-linear")
                .StartWith<ValidatePayment>()
                .Then<ProcessPayment>(step => step
                    .WithRetry(3, TimeSpan.FromSeconds(5))
                    .WithTimeout(TimeSpan.FromMinutes(2)))
                .Then<AuditPayment>(step => step.WithRetry(2))
                .Finally<SendReceipt>();
        }
        """;

    /// <summary>
    /// A linear workflow whose <c>ProcessPayment</c> step declares an out-of-range timeout literal
    /// (<c>WithTimeout(TimeSpan.FromDays(1e18))</c>), probing F3 (TimeSpan.From* overflow must not
    /// crash the parser).
    /// </summary>
    private const string ExtremeTimeoutWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PaymentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidatePayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class SendReceipt : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        [Workflow("extreme-timeout")]
        public static partial class ExtremeTimeoutWorkflow
        {
            public static WorkflowDefinition<PaymentState> Definition => Workflow<PaymentState>
                .Create("extreme-timeout")
                .StartWith<ValidatePayment>()
                .Then<ProcessPayment>(step => step.WithTimeout(TimeSpan.FromDays(1e18)))
                .Finally<SendReceipt>();
        }
        """;

    /// <summary>
    /// A linear workflow whose <c>ProcessPayment</c> step declares a unary-minus retry literal
    /// (<c>WithRetry(-1)</c>), probing F2 (negative retry literal must reach the IR).
    /// </summary>
    private const string NegativeRetryWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record PaymentState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidatePayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class ProcessPayment : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        public class SendReceipt : IWorkflowStep<PaymentState>
        {
            public Task<StepResult<PaymentState>> ExecuteAsync(
                PaymentState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<PaymentState>.FromState(state));
        }

        [Workflow("negative-retry")]
        public static partial class NegativeRetryWorkflow
        {
            public static WorkflowDefinition<PaymentState> Definition => Workflow<PaymentState>
                .Create("negative-retry")
                .StartWith<ValidatePayment>()
                .Then<ProcessPayment>(step => step.WithRetry(-1))
                .Finally<SendReceipt>();
        }
        """;

    /// <summary>
    /// A workflow whose step declares compensation and confidence gating via the
    /// configure lambda, exercising <c>Compensate&lt;T&gt;</c> (FQN), <c>RequireConfidence</c>,
    /// and <c>OnLowConfidence</c>.
    /// </summary>
    private const string ResilienceCompensationConfidenceWorkflow = """
        using System;
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

        public class RollbackAssessment : IWorkflowStep<ClaimState>
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

        [Workflow("resilience-claim")]
        public static partial class ResilienceClaimWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("resilience-claim")
                .StartWith<IntakeClaim>()
                .Then<AssessClaim>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<HumanReview>())
                    .Compensate<RollbackAssessment>())
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A workflow whose gated step declares a TWO-step <c>OnLowConfidence</c> chain
    /// (<c>alt.Then&lt;HumanReview&gt;().Then&lt;EscalateReview&gt;()</c>) with no rejoin
    /// marker (G-4 / #139).
    /// </summary>
    private const string ConfidenceChainWorkflow = """
        using System;
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

        public class EscalateReview : IWorkflowStep<ClaimState>
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

        [Workflow("confidence-chain")]
        public static partial class ConfidenceChainWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("confidence-chain")
                .StartWith<IntakeClaim>()
                .Then<AssessClaim>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt
                        .Then<HumanReview>()
                        .Then<EscalateReview>()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A workflow whose gated step declares a single-step REJOINING
    /// <c>OnLowConfidence</c> handler
    /// (<c>alt.Then&lt;HumanReview&gt;().RejoinMainFlow()</c>) (G-4 / #139).
    /// </summary>
    private const string ConfidenceRejoinWorkflow = """
        using System;
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

        [Workflow("confidence-rejoin")]
        public static partial class ConfidenceRejoinWorkflow
        {
            public static WorkflowDefinition<ClaimState> Definition => Workflow<ClaimState>
                .Create("confidence-rejoin")
                .StartWith<IntakeClaim>()
                .Then<AssessClaim>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt
                        .Then<HumanReview>()
                        .RejoinMainFlow()))
                .Finally<SettleClaim>();
        }
        """;

    /// <summary>
    /// A workflow with a <c>RepeatUntil</c> loop whose body step declares retry/timeout via
    /// the configure lambda, exercising loop-body resilience parse parity.
    /// </summary>
    private const string ResilienceLoopWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record DraftState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
            public decimal QualityScore { get; init; }
        }

        public class StartDraft : IWorkflowStep<DraftState>
        {
            public Task<StepResult<DraftState>> ExecuteAsync(
                DraftState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DraftState>.FromState(state));
        }

        public class RefineDraft : IWorkflowStep<DraftState>
        {
            public Task<StepResult<DraftState>> ExecuteAsync(
                DraftState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DraftState>.FromState(state));
        }

        public class PublishDraft : IWorkflowStep<DraftState>
        {
            public Task<StepResult<DraftState>> ExecuteAsync(
                DraftState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<DraftState>.FromState(state));
        }

        [Workflow("resilience-loop")]
        public static partial class ResilienceLoopWorkflow
        {
            public static WorkflowDefinition<DraftState> Definition => Workflow<DraftState>
                .Create("resilience-loop")
                .StartWith<StartDraft>()
                .RepeatUntil(
                    condition: state => state.QualityScore >= 0.9m,
                    loopName: "Refinement",
                    body: loop => loop
                        .Then<RefineDraft>(step => step
                            .WithRetry(4, TimeSpan.FromSeconds(3))
                            .WithTimeout(TimeSpan.FromMinutes(1))),
                    maxIterations: 5)
                .Finally<PublishDraft>();
        }
        """;

    /// <summary>
    /// A workflow with a <c>Fork</c> whose path step declares retry/timeout via the configure
    /// lambda, exercising fork-path resilience parse parity.
    /// </summary>
    private const string ResilienceForkWorkflow = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record CheckoutState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateCart : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class ChargeCard : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class ReserveStock : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class FinalizeOrder : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        public class CompleteCheckout : IWorkflowStep<CheckoutState>
        {
            public Task<StepResult<CheckoutState>> ExecuteAsync(
                CheckoutState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<CheckoutState>.FromState(state));
        }

        [Workflow("resilience-fork")]
        public static partial class ResilienceForkWorkflow
        {
            public static WorkflowDefinition<CheckoutState> Definition => Workflow<CheckoutState>
                .Create("resilience-fork")
                .StartWith<ValidateCart>()
                .Fork(
                    path => path.Then<ChargeCard>(step => step
                        .WithRetry(5, TimeSpan.FromSeconds(2))
                        .WithTimeout(TimeSpan.FromSeconds(30))),
                    path => path.Then<ReserveStock>())
                .Join<FinalizeOrder>()
                .Finally<CompleteCheckout>();
        }
        """;
}
