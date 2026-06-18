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
}
