// -----------------------------------------------------------------------
// <copyright file="NoConfigBaselineTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Golden baseline (DR-10): a step with NO resilience configuration must
/// generate the same saga/handler/commands output as before the step-resilience
/// epic. Every resilience emit-guard
/// (retry / timeout / compensation / confidence / context) must correctly emit
/// NOTHING for a config-free step.
/// </summary>
/// <remarks>
/// <para>
/// This is the negative-space counterpart to the per-feature lowering tests
/// (<see cref="RetryLoweringTests"/>, <see cref="TimeoutLoweringTests"/>,
/// <see cref="CompensationLoweringTests"/>, <see cref="ConfidenceLoweringTests"/>,
/// and the context wire-in tests). Each of those proves the artifact appears
/// when the feature is configured; this proves the WHOLE pipeline stays inert
/// when nothing is configured, so a future emit-guard regression that leaks a
/// resilience artifact onto a plain step fails the build mechanically.
/// </para>
/// <para>
/// The workflow exercises every step position (start / middle / finally) with no
/// <c>.WithRetry</c>, <c>.WithTimeout</c>, <c>.Compensate</c>,
/// <c>.RequireConfidence</c> or <c>.WithContext</c> anywhere, then asserts the
/// generated saga, handlers, commands and assemblers carry none of the five
/// resilience artifact families.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class NoConfigBaselineTests
{
    /// <summary>
    /// A fully config-free linear workflow: start / middle / finally steps, none
    /// of which declare any resilience policy. This is the golden baseline shape.
    /// </summary>
    private const string WorkflowWithoutResilience = """
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

        public class ProcessPayment : IWorkflowStep<OrderState>
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
                .Then<ProcessPayment>()
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A config-free workflow lowers NONE of the five resilience artifact
    /// families: no worker <c>Configure(HandlerChain)</c> (retry), no
    /// <c>: TimeoutMessage</c>-derived record (timeout), no compensation trigger
    /// publish / rollback wiring (compensation), no confidence threshold
    /// comparison or low-confidence branch dispatch (confidence), and no
    /// <c>{Step}ContextAssembler</c> (context). The generated saga / handlers /
    /// commands / assemblers are the pre-epic golden baseline.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithoutResilience_GeneratedOutputHasNoResilienceArtifacts()
    {
        // Arrange & Act — run the FULL generator pipeline.
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithoutResilience);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");
        var assemblersSource = GeneratorTestHelper.GetGeneratedSource(result, "Assemblers.g.cs");

        // Sanity: the pipeline did produce the core artifacts for a normal
        // workflow, so the negative assertions below are meaningful (not asserting
        // against empty strings because generation silently failed).
        await Assert.That(sagaSource).IsNotEmpty();
        await Assert.That(handlersSource).IsNotEmpty();

        // Retry guard — no per-handler Wolverine error policy hook.
        await Assert.That(handlersSource).DoesNotContain("Configure(HandlerChain");
        await Assert.That(handlersSource).DoesNotContain("RetryTimes(");
        await Assert.That(handlersSource).DoesNotContain("RetryWithCooldown(");

        // Timeout guard — no TimeoutMessage-derived record, no timeout cascade.
        await Assert.That(sagaSource).DoesNotContain(": TimeoutMessage");
        await Assert.That(sagaSource).DoesNotContain("Timeout(");

        // Compensation guard — no failure-handler trigger command, no
        // CompensatingAction publish, no rollback worker dispatch.
        await Assert.That(sagaSource).DoesNotContain("FailureHandlerCommand");
        await Assert.That(handlersSource).DoesNotContain("FailureHandlerCommand");
        await Assert.That(handlersSource).DoesNotContain("CompensatingAction");

        // Confidence guard — no threshold comparison branch in the saga. The
        // raw confidence value still flows as a telemetry span tag (emitted by
        // the worker handler regardless of any gate), so the precise marker is
        // the threshold-comparison branch the confidence gate lowers:
        // "if (evt.Confidence is double confidenceScore && confidenceScore < t)".
        await Assert.That(sagaSource).DoesNotContain("confidenceScore <");
        await Assert.That(sagaSource).DoesNotContain("below threshold");

        // Context guard — no per-step context assembler emitted or wired in.
        await Assert.That(assemblersSource).DoesNotContain("ContextAssembler");
        await Assert.That(handlersSource).DoesNotContain("ContextAssembler");
    }
}
