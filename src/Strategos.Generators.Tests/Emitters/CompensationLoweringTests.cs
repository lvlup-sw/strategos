// -----------------------------------------------------------------------
// <copyright file="CompensationLoweringTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Shape/regression tests for lowering a step's <c>.Compensate&lt;T&gt;()</c>
/// rollback into a runnable compensation path (DR-3).
/// </summary>
/// <remarks>
/// <para>
/// These tests run the FULL workflow generator pipeline (not just one emitter)
/// so they double as a golden/shape regression check. A step configured with a
/// compensation must lower into:
/// <list type="bullet">
///   <item><description>
///     a worker <c>Configure(HandlerChain)</c> error chain that, on terminal
///     failure (after any retries), PUBLISHES the
///     <c>Trigger{Pascal}FailureHandlerCommand</c> via
///     <c>.Then.CompensatingAction&lt;Execute{Step}WorkerCommand&gt;(...)</c>;
///   </description></item>
///   <item><description>
///     a saga trigger handler that, on receiving that command, dispatches the
///     compensation step's worker command (so the rollback step actually RUNS);
///   </description></item>
///   <item><description>
///     a worker handler for the compensation step type (it is folded into the
///     emitted step types so its proven main-flow worker dispatch is reused);
///   </description></item>
///   <item><description>
///     a saga completion handler that, when the compensation step completes,
///     transitions the saga to its terminal <c>Failed</c> phase.
///   </description></item>
/// </list>
/// A step WITHOUT compensation must lower none of these artifacts.
/// </para>
/// </remarks>
[Property("Category", "Integration")]
public class CompensationLoweringTests
{
    /// <summary>
    /// A linear workflow whose middle step declares <c>.WithRetry(2)</c> and
    /// <c>.Compensate&lt;RollbackPayment&gt;()</c> via the step-configure lambda.
    /// The first and last steps declare no resilience.
    /// </summary>
    private const string WorkflowWithStepCompensation = """
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

        public class RollbackPayment : IWorkflowStep<OrderState>
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
                    .WithRetry(2)
                    .Compensate<RollbackPayment>())
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A linear workflow with NO compensation configured on any step.
    /// </summary>
    private const string WorkflowWithoutCompensation = """
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
    /// A step with <c>.Compensate&lt;T&gt;()</c> lowers, onto the compensated
    /// step's worker <c>Configure(HandlerChain)</c>, an error chain that PUBLISHES
    /// the trigger failure-handler command via
    /// <c>.Then.CompensatingAction&lt;Execute{Step}WorkerCommand&gt;(...)</c> after
    /// retries are exhausted. This closes the dead path: the previously
    /// never-published trigger command now reaches the saga at runtime.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithCompensate_ConfigureChainPublishesTriggerFailureHandlerCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithStepCompensation);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");

        // Assert — the compensated step's handler carries a Configure chain that,
        // after the retry policy, publishes the trigger command on terminal failure.
        await Assert.That(handlersSource).Contains("public static void Configure(HandlerChain");
        await Assert.That(handlersSource).Contains(".Then");
        await Assert.That(handlersSource).Contains("CompensatingAction<ExecuteProcessPaymentWorkerCommand>");
        await Assert.That(handlersSource).Contains("TriggerProcessOrderFailureHandlerCommand");
        await Assert.That(handlersSource).Contains("InvokeResult.Stop");

        // The retry policy is still lowered (the prefix of the same chain).
        await Assert.That(handlersSource).Contains("RetryTimes(2)");
    }

    /// <summary>
    /// The compensation step is REACHABLE: the saga's trigger handler dispatches
    /// the rollback step's worker command, a worker handler exists for the
    /// rollback step type, and the rollback's completion routes the saga to its
    /// terminal <c>Failed</c> phase.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithCompensate_CompensationStepIsReachableAndRoutesToFailed()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithStepCompensation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");

        // The saga handles the trigger command and dispatches the rollback worker.
        await Assert.That(sagaSource).Contains("TriggerProcessOrderFailureHandlerCommand cmd");
        await Assert.That(sagaSource).Contains("new ExecuteRollbackPaymentWorkerCommand(WorkflowId");

        // A worker handler exists for the compensation step type so it actually runs.
        await Assert.That(handlersSource).Contains("class RollbackPaymentHandler");

        // The rollback's completion routes the saga to its terminal Failed phase.
        await Assert.That(sagaSource).Contains("RollbackPaymentCompleted evt");
        await Assert.That(sagaSource).Contains("Phase = ProcessOrderPhase.Failed");
    }

    /// <summary>
    /// A workflow with no compensation on any step lowers no compensation
    /// artifacts whatsoever: no <c>CompensatingAction</c>, and no rollback worker.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_StepWithoutCompensate_GeneratesNoCompensationArtifacts()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithoutCompensation);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");

        // Assert — no CompensatingAction publish, no trigger command, no rollback worker.
        await Assert.That(handlersSource).DoesNotContain("CompensatingAction");
        await Assert.That(sagaSource).DoesNotContain("FailureHandlerCommand");
    }
}
