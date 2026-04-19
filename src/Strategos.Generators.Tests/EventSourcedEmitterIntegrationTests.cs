// -----------------------------------------------------------------------
// <copyright file="EventSourcedEmitterIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for event-sourced persistence mode code generation.
/// </summary>
[Property("Category", "Integration")]
public class EventSourcedEmitterIntegrationTests
{
    /// <summary>
    /// Source code for a simple event-sourced workflow.
    /// </summary>
    private const string EventSourcedLinearWorkflow = """
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

        [Workflow("process-order", Persistence = PersistenceMode.EventSourced)]
        public static partial class ProcessOrderWorkflow
        {
            public static WorkflowDefinition<OrderState> Definition => Workflow<OrderState>
                .Create("process-order")
                .StartWith<ValidateOrder>()
                .Then<ProcessPayment>()
                .Finally<SendConfirmation>();
        }
        """;

    // =============================================================================
    // A. Saga Generation — Event-Sourced Mode
    // =============================================================================

    /// <summary>
    /// Verifies that event-sourced saga includes IDocumentSession using.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_IncludesMartenUsing()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("using Marten;");
    }

    /// <summary>
    /// Verifies that event-sourced handlers include IDocumentSession parameter.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_HandlersIncludeDocumentSessionParameter()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("IDocumentSession session,");
    }

    /// <summary>
    /// Verifies that event-sourced handlers include session null guard.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_HandlersIncludeSessionNullGuard()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("ArgumentNullException.ThrowIfNull(session, nameof(session));");
    }

    /// <summary>
    /// Verifies that event-sourced handlers append events to the stream.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_HandlersAppendEventsToStream()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("session.Events.Append(WorkflowId, evt);");
    }

    /// <summary>
    /// Verifies that event-sourced handlers call State.ApplyEvent instead of Reducer.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_HandlersCallApplyEventInsteadOfReducer()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("State = State.ApplyEvent(evt);");
        await Assert.That(sagaSource).DoesNotContain("Reduce(State, evt.UpdatedState)");
    }

    /// <summary>
    /// Verifies that event-sourced saga still extends Saga base class.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_StillExtendsSagaBaseClass()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains(": Saga");
    }

    /// <summary>
    /// Verifies that event-sourced saga still calls MarkCompleted for final step.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_FinalStepCallsMarkCompleted()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("MarkCompleted();");
    }

    // =============================================================================
    // B. Extensions Generation — Snapshot Configuration
    // =============================================================================

    /// <summary>
    /// Verifies that event-sourced extensions include Marten snapshot configuration.
    /// </summary>
    [Test]
    public async Task EventSourced_Extensions_IncludesSnapshotConfiguration()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var extSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        await Assert.That(extSource).Contains("Snapshot<OrderState>");
    }

    /// <summary>
    /// Verifies that event-sourced extensions include Marten usings.
    /// </summary>
    [Test]
    public async Task EventSourced_Extensions_IncludesMartenUsings()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var extSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        await Assert.That(extSource).Contains("using Marten;");
        await Assert.That(extSource).Contains("using Marten.Events.Projections;");
    }

    // =============================================================================
    // B2. Non-Linear Workflow — Loop Coverage
    // =============================================================================

    /// <summary>
    /// Source code for an event-sourced workflow with a loop.
    /// Exercises LoopCompletedHandlerEmitter event-sourced path.
    /// </summary>
    private const string EventSourcedLoopWorkflow = """
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
                => Task.FromResult(StepResult<RefinementState>.FromState(state));
        }

        public class RefineStep : IWorkflowStep<RefinementState>
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

        [Workflow("iterative-refinement", Persistence = PersistenceMode.EventSourced)]
        public static partial class IterativeRefinementWorkflow
        {
            public static WorkflowDefinition<RefinementState> Definition => Workflow<RefinementState>
                .Create("iterative-refinement")
                .StartWith<ValidateInput>()
                .RepeatUntil(
                    state => state.QualityScore >= 0.9m,
                    "Refinement",
                    loop => loop
                        .Then<CritiqueStep>()
                        .Then<RefineStep>(),
                    maxIterations: 5)
                .Finally<PublishResult>();
        }
        """;

    /// <summary>
    /// Verifies that event-sourced loop workflow generates ApplyEvent in loop handler.
    /// </summary>
    [Test]
    public async Task EventSourced_LoopWorkflow_HandlersUseApplyEvent()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLoopWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementSaga.g.cs");

        await Assert.That(sagaSource).Contains("session.Events.Append(WorkflowId, evt);");
        await Assert.That(sagaSource).Contains("State = State.ApplyEvent(evt);");
        await Assert.That(sagaSource).Contains("IDocumentSession session,");
        await Assert.That(sagaSource).DoesNotContain("Reduce(State, evt.UpdatedState)");
    }

    /// <summary>
    /// Verifies that event-sourced loop workflow generates snapshot config.
    /// </summary>
    [Test]
    public async Task EventSourced_LoopWorkflow_ExtensionsIncludeSnapshot()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLoopWorkflow);
        var extSource = GeneratorTestHelper.GetGeneratedSource(result, "IterativeRefinementExtensions.g.cs");

        await Assert.That(extSource).Contains("Snapshot<RefinementState>");
    }

    // =============================================================================
    // C. XML Documentation
    // =============================================================================

    /// <summary>
    /// Verifies that event-sourced handlers include session param XML doc.
    /// </summary>
    [Test]
    public async Task EventSourced_Saga_HandlersIncludeSessionXmlDoc()
    {
        var result = GeneratorTestHelper.RunGenerator(EventSourcedLinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("<param name=\"session\">");
    }

    // =============================================================================
    // D. Validation Diagnostics
    // =============================================================================

    /// <summary>
    /// Verifies that event-sourced mode without state type emits a diagnostic.
    /// </summary>
    [Test]
    public async Task EventSourced_WithoutStateType_EmitsDiagnostic()
    {
        const string source = """
            using Strategos.Attributes;

            namespace TestNamespace;

            [Workflow("no-state", Persistence = PersistenceMode.EventSourced)]
            public static partial class NoStateWorkflow
            {
            }
            """;

        var result = GeneratorTestHelper.RunGenerator(source);
        var diagnostics = result.Diagnostics;

        await Assert.That(diagnostics).Count().IsGreaterThanOrEqualTo(1);
        await Assert.That(diagnostics.Any(d => d.Id == "AGWF016")).IsTrue();
    }

    // =============================================================================
    // E. Default Mode (SagaDocument) — Backward Compatibility
    // =============================================================================

    /// <summary>
    /// Verifies that default mode does NOT include IDocumentSession parameter.
    /// </summary>
    [Test]
    public async Task Default_Saga_DoesNotIncludeDocumentSessionParameter()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).DoesNotContain("IDocumentSession");
    }

    /// <summary>
    /// Verifies that default mode uses Reducer pattern.
    /// </summary>
    [Test]
    public async Task Default_Saga_UsesReducerPattern()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderSaga.g.cs");

        await Assert.That(sagaSource).Contains("Reduce(State, evt.UpdatedState)");
        await Assert.That(sagaSource).DoesNotContain("session.Events.Append");
        await Assert.That(sagaSource).DoesNotContain("State.ApplyEvent");
    }

    /// <summary>
    /// Verifies that default mode does NOT include snapshot configuration.
    /// </summary>
    [Test]
    public async Task Default_Extensions_DoesNotIncludeSnapshotConfiguration()
    {
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var extSource = GeneratorTestHelper.GetGeneratedSource(result, "ProcessOrderExtensions.g.cs");

        await Assert.That(extSource).DoesNotContain("Snapshot<");
        await Assert.That(extSource).DoesNotContain("using Marten;");
    }
}
