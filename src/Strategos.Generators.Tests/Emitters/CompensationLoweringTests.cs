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
    /// A linear workflow whose compensation target step type (<c>RollbackStep</c>) is
    /// ALSO used as a normal main-flow step. The main flow runs
    /// <c>ValidateOrder → RollbackStep → ProcessPayment → SendConfirmation</c>, and
    /// <c>ProcessPayment</c> declares <c>.Compensate&lt;RollbackStep&gt;()</c>. Because the
    /// rollback type is already a main-flow step it must NOT get a second, duplicate
    /// <c>Handle(RollbackStepCompleted)</c> from the compensation emitter (CS0111).
    /// </summary>
    private const string WorkflowWithCompensationStepAlsoInMainFlow = """
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

        public class RollbackStep : IWorkflowStep<OrderState>
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
                .Then<RollbackStep>()
                .Then<ProcessPayment>(step => step
                    .WithRetry(2)
                    .Compensate<RollbackStep>())
                .Finally<SendConfirmation>();
        }
        """;

    /// <summary>
    /// A linear workflow with TWO distinct compensation step types, so the saga
    /// trigger handler lowers MULTI-compensation routing (a <c>FailedStepName</c>
    /// branch per compensated step). Used to prove the routing has a terminal
    /// fallback for an unmatched <c>FailedStepName</c> (F2).
    /// </summary>
    private const string WorkflowWithTwoCompensations = """
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

        public class ReserveInventory : IWorkflowStep<OrderState>
        {
            public Task<StepResult<OrderState>> ExecuteAsync(
                OrderState state, StepContext context, CancellationToken ct)
                => Task.FromResult(StepResult<OrderState>.FromState(state));
        }

        public class ReleaseInventory : IWorkflowStep<OrderState>
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
                .Then<ReserveInventory>(step => step
                    .Compensate<ReleaseInventory>())
                .Then<ProcessPayment>(step => step
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
    /// F2 (CodeRabbit / epic #135): in multi-compensation routing the saga trigger
    /// handler emits one <c>if (cmd.FailedStepName == "X") { ... yield break; }</c>
    /// branch per compensated step, after setting <c>Phase = Compensating</c>. If
    /// NONE of the branches match an unexpected <c>FailedStepName</c>, the handler
    /// previously fell off the end yielding no command, stranding the saga in the
    /// <c>Compensating</c> phase forever. The routing must have a terminal fallback
    /// (route to <c>Failed</c> + <c>MarkCompleted()</c>) so an unmatched
    /// <c>FailedStepName</c> cannot deadlock the saga.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_MultiCompensationRouting_HasTerminalFallbackForUnmatchedFailedStep()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithTwoCompensations);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Isolate the trigger handler region so the fallback assertion is scoped to
        // this method and not satisfied by an unrelated completed handler.
        var triggerHandler = ExtractTriggerHandlerRegion(sagaSource);

        // Sanity: this is genuinely the multi-compensation routing form (a branch
        // per compensated step routing on FailedStepName).
        await Assert.That(triggerHandler).Contains("cmd.FailedStepName ==");
        await Assert.That(triggerHandler).Contains("ExecuteReleaseInventoryWorkerCommand");
        await Assert.That(triggerHandler).Contains("ExecuteRollbackPaymentWorkerCommand");

        // The terminal fallback: when no FailedStepName branch matches, the handler
        // must transition to Failed and mark the saga completed (no infinite wait).
        await Assert.That(triggerHandler).Contains("Phase = ProcessOrderPhase.Failed");
        await Assert.That(triggerHandler).Contains("MarkCompleted()");

        // The fallback must come AFTER the routing branches (it is the else/last
        // resort), not before them.
        var lastRouteIndex = triggerHandler.LastIndexOf("cmd.FailedStepName ==", StringComparison.Ordinal);
        var markCompletedIndex = triggerHandler.IndexOf("MarkCompleted()", StringComparison.Ordinal);
        await Assert.That(lastRouteIndex).IsGreaterThanOrEqualTo(0);
        await Assert.That(markCompletedIndex).IsGreaterThan(lastRouteIndex);
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

    /// <summary>
    /// When a step TYPE is used both as a normal main-flow step AND as another
    /// step's <c>.Compensate&lt;T&gt;()</c> target, the saga must declare exactly
    /// ONE <c>Handle({Comp}Completed)</c> overload. The main-flow completed handler
    /// (emitted from <c>SagaStepHandlersEmitter</c>) already covers it; the
    /// compensation emitter must NOT emit a second, duplicate overload (CS0111).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_CompensationStepAlsoInMainFlow_EmitsSingleCompletedHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(WorkflowWithCompensationStepAlsoInMainFlow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        // Assert — exactly one `Handle(RollbackStepCompleted evt, ...)` overload.
        // Both the main-flow completed handler and the (now-suppressed) compensation
        // completed handler emit a `Handle(` overload taking `RollbackStepCompleted`;
        // counting that multiline parameter pattern is the duplicate proxy. (The
        // separate `NotFound(RollbackStepCompleted evt, ...)` method is a different
        // method name and is excluded by anchoring on `Handle(`.)
        var occurrences = CountOccurrences(sagaSource, "Handle(\n        RollbackStepCompleted evt,");
        await Assert.That(occurrences).IsEqualTo(1);
    }

    /// <summary>
    /// The generated saga for a workflow that reuses a step type as a compensation
    /// target must COMPILE without a CS0111 duplicate-method error (the concrete
    /// failure mode of the duplicate <c>Handle({Comp}Completed)</c> overload).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Emit_CompensationStepAlsoInMainFlow_GeneratedSagaHasNoCs0111()
    {
        // Arrange & Act — compile the source PLUS the generator output together.
        var diagnostics = GeneratorTestHelper.GetCompilationDiagnostics(
            WorkflowWithCompensationStepAlsoInMainFlow);

        // Assert — no CS0111 (duplicate member) anywhere in the generated saga.
        // Other diagnostics (missing Wolverine/Marten runtime types) are expected
        // in the bare test compilation and are not asserted on.
        var cs0111 = diagnostics
            .Where(d => string.Equals(d.Id, "CS0111", StringComparison.Ordinal))
            .Select(d => d.GetMessage())
            .ToList();

        await Assert.That(cs0111).IsEmpty();
    }

    /// <summary>
    /// Slices out the compensation trigger handler body from the generated saga,
    /// from its <c>Trigger...FailureHandlerCommand cmd,</c> parameter up to the
    /// next member's XML doc comment, so a test asserts on this handler alone.
    /// </summary>
    private static string ExtractTriggerHandlerRegion(string sagaSource)
    {
        var start = sagaSource.IndexOf("FailureHandlerCommand cmd,", StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var next = sagaSource.IndexOf("/// <summary>", start, StringComparison.Ordinal);
        return next < 0 ? sagaSource.Substring(start) : sagaSource.Substring(start, next - start);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
