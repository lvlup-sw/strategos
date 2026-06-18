// -----------------------------------------------------------------------
// <copyright file="TimeoutLoweringTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Shape/regression tests for lowering a step's <c>.WithTimeout(t)</c> into a
/// Wolverine saga <see cref="global::Wolverine.TimeoutMessage"/> (DR-4).
/// </summary>
/// <remarks>
/// <para>
/// These tests run the FULL workflow generator pipeline (not just one emitter)
/// so they double as a golden/shape regression check: a step configured with a
/// timeout must lower into
/// <list type="bullet">
///   <item><description>
///     a <c>{Step}Timeout</c> record deriving from <c>Wolverine.TimeoutMessage</c>
///     (Wolverine auto-schedules its delayed delivery from the base ctor's
///     <see cref="System.TimeSpan"/>);
///   </description></item>
///   <item><description>
///     a cascade of that timeout message from the step-start handler alongside
///     the worker command (so the deadline race starts when the step starts);
///   </description></item>
///   <item><description>
///     a saga <c>Handle({Step}Timeout)</c> that routes to the failure path only
///     when the step's phase has not advanced (idempotent race guard).
///   </description></item>
/// </list>
/// A step WITHOUT a timeout must lower no timeout artifacts at all.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class TimeoutLoweringTests
{
    /// <summary>
    /// A linear workflow whose middle step declares <c>.WithTimeout(...)</c> via
    /// the step-configure lambda. The first and last steps declare no resilience.
    /// </summary>
    private const string WorkflowWithStepTimeout = """
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
                .Then<ProcessPayment>(step => step
                    .WithTimeout(TimeSpan.FromSeconds(30)))
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A linear workflow with NO timeout configured on any step.
    /// </summary>
    private const string WorkflowWithoutTimeout = """
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
    /// Verifies a step with a timeout lowers into a <c>TimeoutMessage</c>-derived
    /// record, a saga handler for it, and a cascade of that message from the
    /// step-start handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithTimeout_GeneratesTimeoutMessageRecordAndSagaHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithStepTimeout);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert — the timeout message record derives from Wolverine.TimeoutMessage,
        // carries the step name, and is scheduled from the configured 30s duration.
        await Assert.That(sagaSource).Contains("ProcessPaymentTimeout");
        await Assert.That(sagaSource).Contains(": TimeoutMessage");

        // The step-start handler cascades the timeout message alongside the worker
        // command so the deadline race begins when the step begins.
        await Assert.That(sagaSource).Contains("new ProcessPaymentTimeout(");

        // The saga handles the timeout later (race guard / failure routing).
        await Assert.That(sagaSource).Contains("Handle(");
        await Assert.That(sagaSource).Contains("ProcessPaymentTimeout t");
    }

    /// <summary>
    /// Verifies a workflow with no timeout on any step lowers no timeout
    /// artifacts whatsoever.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithoutTimeout_GeneratesNoTimeoutMessage()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithoutTimeout);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert — no timeout message, no derivation, no cascade.
        await Assert.That(sagaSource).DoesNotContain(": TimeoutMessage");
        await Assert.That(sagaSource).DoesNotContain("Timeout(");
    }
}
