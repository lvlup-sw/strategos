// -----------------------------------------------------------------------
// <copyright file="ConfidenceLoweringTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Shape/regression tests for lowering a step's
/// <c>.RequireConfidence(threshold).OnLowConfidence(alt =&gt; alt.Then&lt;THandler&gt;())</c>
/// into a Wolverine saga routing decision (DR-5).
/// </summary>
/// <remarks>
/// <para>
/// These tests run the FULL workflow generator pipeline (not just one emitter)
/// so they double as a golden/shape regression check. A step configured with a
/// confidence gate must lower into:
/// <list type="bullet">
///   <item><description>
///     a saga completed-handler that compares the completed event's
///     <c>Confidence</c> to the configured threshold;
///   </description></item>
///   <item><description>
///     a dispatch to the low-confidence handler step's start command
///     (<c>Start{Handler}Command</c>) when confidence is below the threshold,
///     instead of routing to the normal next step;
///   </description></item>
///   <item><description>
///     the low-confidence handler step itself being lowered (its own worker
///     handler / start command), so the branch is actually runnable.
///   </description></item>
/// </list>
/// A step WITHOUT a confidence gate must lower no threshold comparison — only
/// the pre-existing telemetry span tag carrying the raw confidence value.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class ConfidenceLoweringTests
{
    /// <summary>
    /// A linear workflow whose middle step declares
    /// <c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;())</c>
    /// via the step-configure lambda. The first and last steps declare no
    /// resilience.
    /// </summary>
    private const string WorkflowWithStepConfidence = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ClassifyIntent : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ClassifyIntent>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<HumanReview>()))
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A linear workflow whose middle step declares BOTH a confidence gate
    /// (<c>.RequireConfidence(0.85).OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;())</c>)
    /// AND a workflow-level <c>.OnFailure(...)</c> handler. The confidence-gated
    /// completed handler must still route to the failure path when the reducer
    /// drives the saga into the <c>Failed</c> phase (F1).
    /// </summary>
    private const string WorkflowWithConfidenceAndFailureHandler = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ClassifyIntent : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class LogFailure : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ClassifyIntent>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt.Then<HumanReview>()))
                .Finally<SendConfirmation>()
                .OnFailure(f => f
                    .Then<LogFailure>()
                    .Complete());
        }
        """;

    /// <summary>
    /// A linear workflow with NO confidence gate configured on any step.
    /// </summary>
    private const string WorkflowWithoutConfidence = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ClassifyIntent : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ClassifyIntent>()
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// Verifies a step with a confidence gate lowers into a saga completed
    /// handler that compares the event's confidence to the configured threshold
    /// and dispatches to the low-confidence handler step's start command, and
    /// that the handler step itself is lowered (so the branch is runnable).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithRequireConfidence_GeneratesConfidenceThresholdBranchDispatch()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithStepConfidence);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert — the saga's ClassifyIntent completed handler compares the
        // event's Confidence to the configured 0.85 threshold (not merely a
        // telemetry span tag).
        await Assert.That(sagaSource).Contains("Confidence");
        await Assert.That(sagaSource).Contains("0.85");

        // When below the threshold the saga routes to the low-confidence handler
        // step's start command instead of the normal next step.
        await Assert.That(sagaSource).Contains("StartHumanReviewCommand");

        // When at/above the threshold the gate proceeds down the normal path to
        // the next step (SendConfirmation), not to the handler.
        await Assert.That(sagaSource).Contains("StartSendConfirmationCommand");

        // The handler step is fully lowered so the branch is runnable: it has its
        // own start command in the generated commands file.
        await Assert.That(commandsSource).Contains("StartHumanReviewCommand");
    }

    /// <summary>
    /// F1 (CodeRabbit / epic #135): a step with BOTH a confidence gate AND a
    /// workflow <c>.OnFailure(...)</c> handler must still route to the failure
    /// path when the reducer drives the saga into the <c>Failed</c> phase. The
    /// confidence-gated completed handler previously skipped the
    /// <c>Phase == Failed</c> route that the phase-aware non-final handler emits,
    /// so a confidence-gated step that set <c>Phase = Failed</c> would still chain
    /// to the next step instead of dispatching the failure handler (INV-1: the
    /// failure route is a Wolverine cascade lowered by the generator).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_ConfidenceGatedStepWithFailureHandler_RoutesFailedPhaseToFailureHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithConfidenceAndFailureHandler);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Isolate the ClassifyIntent confidence-gated completed handler region so
        // the assertions cannot be satisfied spuriously by another step's
        // phase-aware handler (e.g. ValidateOrder, which also emits the Failed
        // guard). The region runs from this handler's parameter to the next
        // member's XML doc comment.
        var handler = ExtractConfidenceHandlerRegion(sagaSource, "ClassifyIntentCompleted evt,");

        // Assert — the confidence comparison (the gate itself) is intact within
        // the isolated handler.
        await Assert.That(handler).Contains("confidenceScore < 0.85");
        await Assert.That(handler).Contains("StartHumanReviewCommand");

        // Assert — the confidence-gated handler ITSELF guards on the Failed phase
        // and routes to the lowered failure handler step (LogFailure). Without the
        // F1 fix this guard is absent from the confidence-gated handler entirely.
        await Assert.That(handler).Contains("if (Phase == ProcessOrderPhase.Failed)");
        await Assert.That(handler).Contains("StartLogFailureCommand");

        // The Failed-phase guard must precede the confidence comparison so a step
        // that BOTH fails AND gates on confidence routes to the failure path, not
        // the low-confidence handler.
        var failedGuardIndex = handler.IndexOf(
            "if (Phase == ProcessOrderPhase.Failed)",
            StringComparison.Ordinal);
        var confidenceCompareIndex = handler.IndexOf(
            "confidenceScore < 0.85",
            StringComparison.Ordinal);
        await Assert.That(failedGuardIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(confidenceCompareIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(failedGuardIndex).IsLessThan(confidenceCompareIndex);
    }

    /// <summary>
    /// Slices out a single completed-handler body from the generated saga, from
    /// the handler's parameter signature up to the next member's XML doc comment.
    /// Lets a test assert on ONE handler in isolation so cross-handler text cannot
    /// satisfy an assertion spuriously.
    /// </summary>
    private static string ExtractConfidenceHandlerRegion(string sagaSource, string handlerParameter)
    {
        var start = sagaSource.IndexOf(handlerParameter, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        // The next member starts at the following "    /// <summary>" doc block.
        var next = sagaSource.IndexOf("/// <summary>", start, StringComparison.Ordinal);
        return next < 0 ? sagaSource.Substring(start) : sagaSource.Substring(start, next - start);
    }

    /// <summary>
    /// Verifies a workflow with no confidence gate on any step lowers no
    /// threshold comparison and no low-confidence dispatch — only the
    /// pre-existing telemetry span tag carrying the raw confidence value (emitted
    /// by the worker handler, not the saga).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithoutConfidence_NoThresholdComparison()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithoutConfidence);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert — the saga performs no confidence threshold comparison and no
        // low-confidence routing.
        await Assert.That(sagaSource).DoesNotContain("evt.Confidence <");
        await Assert.That(sagaSource).DoesNotContain("StartHumanReviewCommand");
    }

    /// <summary>
    /// A linear workflow whose middle step declares a TWO-step OnLowConfidence
    /// chain (<c>OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;().Then&lt;EscalateReview&gt;())</c>),
    /// with no rejoin marker (so the chain terminates). Drives the multi-step
    /// chain lowering (G-4 / #139).
    /// </summary>
    private const string WorkflowWithTwoStepChain = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ClassifyIntent : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class EscalateReview : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ClassifyIntent>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt
                        .Then<HumanReview>()
                        .Then<EscalateReview>()))
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A linear workflow whose middle step declares a single-step REJOINING
    /// OnLowConfidence handler
    /// (<c>OnLowConfidence(alt =&gt; alt.Then&lt;HumanReview&gt;().RejoinMainFlow())</c>).
    /// Drives the rejoin lowering (G-4 / #139): the handler resumes the main flow
    /// at the step after the gated step (SendConfirmation).
    /// </summary>
    private const string WorkflowWithRejoiningHandler = """
        using System;
        using Strategos.Abstractions;
        using Strategos.Attributes;
        using Strategos.Builders;
        using Strategos.Definitions;
        using Strategos.Steps;

        namespace TestNamespace;

        public record OrderState : IWorkflowState
        {
            public Guid WorkflowId { get; init; }
        }

        public class ValidateOrder : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ClassifyIntent : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class HumanReview : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class SendConfirmation : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        [Workflow("process-order")]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ClassifyIntent>(step => step
                    .RequireConfidence(0.85)
                    .OnLowConfidence(alt => alt
                        .Then<HumanReview>()
                        .RejoinMainFlow()))
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// Task 4.1 golden shape: a TWO-step OnLowConfidence chain lowers BOTH handler
    /// steps, with the confidence gate routing to the FIRST (HumanReview), the
    /// first handler chaining to the SECOND (EscalateReview), and the second
    /// (terminating, no rejoin marker) marking the saga completed. Before #139
    /// only one handler step was lowered.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_TwoStepChain_LowersBothHandlerStepsInOrder()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithTwoStepChain);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // The confidence gate routes to the FIRST handler step.
        await Assert.That(sagaSource).Contains("StartHumanReviewCommand");

        // BOTH handler steps are fully lowered (each has its own start command).
        await Assert.That(commandsSource).Contains("StartHumanReviewCommand");
        await Assert.That(commandsSource).Contains("StartEscalateReviewCommand");

        // The first handler step (HumanReview) chains to the second (EscalateReview)
        // rather than terminating: its completed handler returns the second's start
        // command.
        var firstHandler = ExtractConfidenceHandlerRegion(sagaSource, "HumanReviewCompleted evt,");
        await Assert.That(firstHandler).Contains("StartEscalateReviewCommand");

        // The second (last) handler step terminates the (default terminating) chain.
        var secondHandler = ExtractConfidenceHandlerRegion(sagaSource, "EscalateReviewCompleted evt,");
        await Assert.That(secondHandler).Contains("MarkCompleted()");
    }

    /// <summary>
    /// Task 4.2 golden shape (terminating, back-compat default): a single-step
    /// OnLowConfidence handler with no rejoin marker marks the saga completed —
    /// it does NOT chain back to the main flow's next step (SendConfirmation).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_TerminatingHandler_MarksSagaCompleted()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithStepConfidence);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // The handler step's own completed handler marks the saga completed and does
        // NOT route back to the main-flow step after the gate (SendConfirmation).
        var handler = ExtractConfidenceHandlerRegion(sagaSource, "HumanReviewCompleted evt,");
        await Assert.That(handler).Contains("MarkCompleted()");
        await Assert.That(handler).DoesNotContain("StartSendConfirmationCommand");
    }

    /// <summary>
    /// Task 4.2 golden shape (rejoining): a single-step OnLowConfidence handler
    /// declared <c>.RejoinMainFlow()</c> resumes the MAIN flow at the step after the
    /// gated step (SendConfirmation) instead of marking the saga completed.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_RejoiningHandler_ResumesMainFlowAfterGatedStep()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithRejoiningHandler);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // The handler step's own completed handler routes to the main-flow step
        // after the gate (SendConfirmation) and does NOT mark the saga completed.
        var handler = ExtractConfidenceHandlerRegion(sagaSource, "HumanReviewCompleted evt,");
        await Assert.That(handler).Contains("StartSendConfirmationCommand");
        await Assert.That(handler).DoesNotContain("MarkCompleted()");
    }
}
