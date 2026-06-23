// -----------------------------------------------------------------------
// <copyright file="EventsEmitterUnitTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Emitters;
using Strategos.Generators.Models;

namespace Strategos.Generators.Tests.Emitters;

/// <summary>
/// Unit tests for the <see cref="EventsEmitter"/> class.
/// </summary>
/// <remarks>
/// These tests verify event generation in isolation, independent of the source generator.
/// </remarks>
[Property("Category", "Unit")]
public class EventsEmitterUnitTests
{
    // =============================================================================
    // A. Event Interface Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates a workflow-specific event interface.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesEventInterface()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public partial interface IProcessOrderEvent");
    }

    /// <summary>
    /// Verifies that the event interface extends IProgressEvent.
    /// </summary>
    [Test]
    public async Task Emit_EventInterface_ExtendsIProgressEvent()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("IProcessOrderEvent : IProgressEvent");
    }

    // =============================================================================
    // B. Workflow Started Event Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates a Started event for the workflow.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesStartedEvent()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record ProcessOrderStarted");
    }

    /// <summary>
    /// Verifies that the Started event does NOT have SagaIdentity attribute.
    /// Per design: Started events are published during saga creation.
    /// </summary>
    [Test]
    public async Task Emit_StartedEvent_NoSagaIdentityAttribute()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert - Check the Started event specifically doesn't have SagaIdentity
        // The Started event record should NOT have [property: SagaIdentity]
        var startedIndex = source.IndexOf("ProcessOrderStarted", StringComparison.Ordinal);
        var startedSection = startedIndex >= 0 ? source.Substring(startedIndex, Math.Min(200, source.Length - startedIndex)) : "";
        await Assert.That(startedSection).DoesNotContain("[property: SagaIdentity]");
    }

    /// <summary>
    /// Verifies that the Started event has required parameters.
    /// </summary>
    [Test]
    public async Task Emit_StartedEvent_HasRequiredParameters()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Guid WorkflowId");
        await Assert.That(source).Contains("OrderState InitialState");
        await Assert.That(source).Contains("DateTimeOffset Timestamp");
    }

    /// <summary>
    /// Verifies that the Started event implements the workflow event interface.
    /// </summary>
    [Test]
    public async Task Emit_StartedEvent_ImplementsWorkflowInterface()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderStarted(");
        await Assert.That(source).Contains(") : IProcessOrderEvent");
    }

    // =============================================================================
    // C. Step Completed Event Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the emitter generates Completed events for each step.
    /// </summary>
    [Test]
    public async Task Emit_LinearWorkflow_GeneratesStepCompletedEvents()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record ValidateOrderCompleted");
        await Assert.That(source).Contains("public sealed partial record ProcessPaymentCompleted");
        await Assert.That(source).Contains("public sealed partial record SendConfirmationCompleted");
    }

    /// <summary>
    /// Verifies that Step Completed events have [property: SagaIdentity] on WorkflowId.
    /// Per design: Step completed events route back to the saga.
    /// </summary>
    [Test]
    public async Task Emit_StepCompletedEvent_HasSagaIdentityAttribute()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert - Step completed events should have [property: SagaIdentity]
        await Assert.That(source).Contains("[property: SagaIdentity] Guid WorkflowId");
    }

    /// <summary>
    /// Verifies that Step Completed events have UpdatedState parameter with full state.
    /// </summary>
    [Test]
    public async Task Emit_StepCompletedEvent_HasUpdatedStateParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("OrderState UpdatedState");
    }

    /// <summary>
    /// Verifies that Step Completed events have Confidence parameter.
    /// </summary>
    [Test]
    public async Task Emit_StepCompletedEvent_HasConfidenceParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("double? Confidence");
    }

    /// <summary>
    /// Verifies that Step Completed events have StepExecutionId parameter.
    /// </summary>
    [Test]
    public async Task Emit_StepCompletedEvent_HasStepExecutionIdParameter()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Guid StepExecutionId");
    }

    /// <summary>
    /// Verifies that loop-prefixed step names generate properly named Completed events.
    /// </summary>
    [Test]
    public async Task Emit_LoopWorkflow_GeneratesPrefixedEvents()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "iterative-refinement",
            PascalName: "IterativeRefinement",
            Namespace: "TestNamespace",
            StepNames: ["ValidateInput", "Refinement_CritiqueStep", "Refinement_RefineStep", "PublishResult"],
            StateTypeName: "RefinementState");

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("Refinement_CritiqueStepCompleted");
        await Assert.That(source).Contains("Refinement_RefineStepCompleted");
    }

    /// <summary>
    /// Verifies that Step Completed events implement the workflow event interface.
    /// </summary>
    [Test]
    public async Task Emit_StepCompletedEvent_ImplementsWorkflowInterface()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ValidateOrderCompleted(");
        await Assert.That(source).Contains(") : IProcessOrderEvent;");
    }

    // =============================================================================
    // D. Header and Attributes Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the auto-generated header is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesAutoGeneratedHeader()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("// <auto-generated/>");
    }

    /// <summary>
    /// Verifies that nullable enable directive is included.
    /// </summary>
    [Test]
    public async Task Emit_WithValidModel_IncludesNullableEnable()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("#nullable enable");
    }

    /// <summary>
    /// Verifies that the emitter uses the correct namespace.
    /// </summary>
    [Test]
    public async Task Emit_Events_UsesCorrectNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("namespace TestNamespace;");
    }

    /// <summary>
    /// Verifies that Strategos.Agents.Abstractions namespace is imported for IProgressEvent.
    /// </summary>
    [Test]
    public async Task Emit_Events_ImportsProgressEventNamespace()
    {
        // Arrange
        var model = CreateTestModel();

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("using Strategos.Agents.Abstractions;");
    }

    // =============================================================================
    // D. Guard Clause Tests
    // =============================================================================

    /// <summary>
    /// Verifies that null model throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Emit_WithNullModel_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => EventsEmitter.Emit(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // E. ValidationFailed Event Tests (Milestone 9 - Guard Logic Injection)
    // =============================================================================

    /// <summary>
    /// Verifies that ValidationFailed event is generated when workflow has validation guards.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithValidation_GeneratesValidationFailedEvent()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment", ValidationPredicate: "state.Total > 0", ValidationErrorMessage: "Total must be positive"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            Steps: steps);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record ProcessOrderValidationFailed");
    }

    /// <summary>
    /// Verifies that ValidationFailed event is NOT generated when workflow has no validation.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithoutValidation_DoesNotGenerateValidationFailedEvent()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            Steps: steps);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).DoesNotContain("ValidationFailed");
    }

    /// <summary>
    /// Verifies that ValidationFailed event has correct parameters.
    /// </summary>
    [Test]
    public async Task Emit_ValidationFailedEvent_HasCorrectParameters()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ValidateOrder", "TestNamespace.ValidateOrder"),
            new("ProcessPayment", "TestNamespace.ProcessPayment", ValidationPredicate: "state.Total > 0", ValidationErrorMessage: "Total must be positive"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            Steps: steps);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert - ValidationFailed event should have WorkflowId, StepName, ErrorMessage, Timestamp
        await Assert.That(source).Contains("[property: SagaIdentity] Guid WorkflowId");
        await Assert.That(source).Contains("string StepName");
        await Assert.That(source).Contains("string ErrorMessage");
    }

    /// <summary>
    /// Verifies that ValidationFailed event implements the workflow event interface.
    /// </summary>
    [Test]
    public async Task Emit_ValidationFailedEvent_ImplementsWorkflowInterface()
    {
        // Arrange
        var steps = new List<StepModel>
        {
            new("ProcessPayment", "TestNamespace.ProcessPayment", ValidationPredicate: "state.Total > 0", ValidationErrorMessage: "Total must be positive"),
        };

        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ProcessPayment"],
            StateTypeName: "OrderState",
            Steps: steps);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("ProcessOrderValidationFailed(");
        await Assert.That(source).Contains(") : IProcessOrderEvent;");
    }

    // =============================================================================
    // F. Approval Step Completed Event Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Completed event is generated for approval rejection steps.
    /// </summary>
    [Test]
    public async Task Emit_WithRejectionSteps_GeneratesCompletedEvent()
    {
        // Arrange
        var rejectionSteps = new List<StepModel>
        {
            StepModel.Create("TerminateStep", "TestNamespace.Steps.TerminateStep"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            rejectionSteps: rejectionSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("TerminateStepCompleted");
    }

    /// <summary>
    /// Verifies that Completed event is generated for approval escalation steps.
    /// </summary>
    [Test]
    public async Task Emit_WithEscalationSteps_GeneratesCompletedEvent()
    {
        // Arrange
        var escalationSteps = new List<StepModel>
        {
            StepModel.Create("AutoFailStep", "TestNamespace.Steps.AutoFailStep"),
        };
        var approval = ApprovalModel.Create(
            approvalPointName: "ManagerReview",
            approverTypeName: "TestNamespace.ManagerApprover",
            precedingStepName: "ValidateOrder",
            escalationSteps: escalationSteps);
        var model = CreateTestModelWithApproval(approval);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("AutoFailStepCompleted");
    }

    // =============================================================================
    // G. WorkflowFailed Event Tests (Task E2 - Failure Event Enhancement)
    // =============================================================================

    /// <summary>
    /// Verifies that WorkflowFailed event is generated when workflow has failure handlers.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithFailureHandlers_GeneratesWorkflowFailedEvent()
    {
        // Arrange
        var handler = FailureHandlerModel.Create(
            handlerId: "default-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["LogFailure"],
            isTerminal: true);
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            FailureHandlers: [handler]);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("public sealed partial record ProcessOrderFailed");
    }

    /// <summary>
    /// Verifies that WorkflowFailed event has FailedStepName property.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowFailedEvent_HasFailedStepNameProperty()
    {
        // Arrange
        var handler = FailureHandlerModel.Create(
            handlerId: "default-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["LogFailure"],
            isTerminal: true);
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            FailureHandlers: [handler]);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("string FailedStepName");
    }

    /// <summary>
    /// Verifies that WorkflowFailed event has ExceptionType property.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowFailedEvent_HasExceptionTypeProperty()
    {
        // Arrange
        var handler = FailureHandlerModel.Create(
            handlerId: "default-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["LogFailure"],
            isTerminal: true);
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            FailureHandlers: [handler]);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("string? ExceptionType");
    }

    /// <summary>
    /// Verifies that WorkflowFailed event has ExceptionMessage property.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowFailedEvent_HasExceptionMessageProperty()
    {
        // Arrange
        var handler = FailureHandlerModel.Create(
            handlerId: "default-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["LogFailure"],
            isTerminal: true);
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            FailureHandlers: [handler]);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("string? ExceptionMessage");
    }

    /// <summary>
    /// Verifies that WorkflowFailed event has StackTrace property.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowFailedEvent_HasStackTraceProperty()
    {
        // Arrange
        var handler = FailureHandlerModel.Create(
            handlerId: "default-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["LogFailure"],
            isTerminal: true);
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            FailureHandlers: [handler]);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).Contains("string? StackTrace");
    }

    /// <summary>
    /// Verifies that WorkflowFailed event has Timestamp property.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowFailedEvent_HasTimestampProperty()
    {
        // Arrange
        var handler = FailureHandlerModel.Create(
            handlerId: "default-failure",
            scope: FailureHandlerScope.Workflow,
            stepNames: ["LogFailure"],
            isTerminal: true);
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState",
            FailureHandlers: [handler]);

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert - Check that the Failed event section contains Timestamp
        var failedEventIndex = source.IndexOf("ProcessOrderFailed", StringComparison.Ordinal);
        if (failedEventIndex > 0)
        {
            var failedEventSection = source.Substring(failedEventIndex, Math.Min(500, source.Length - failedEventIndex));
            await Assert.That(failedEventSection).Contains("DateTimeOffset Timestamp");
        }
        else
        {
            await Assert.That(source).Contains("DateTimeOffset Timestamp");
        }
    }

    /// <summary>
    /// Verifies that WorkflowFailed event is NOT generated when workflow has no failure handlers.
    /// </summary>
    [Test]
    public async Task Emit_WorkflowWithoutFailureHandlers_DoesNotGenerateWorkflowFailedEvent()
    {
        // Arrange
        var model = new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment"],
            StateTypeName: "OrderState");

        // Act
        var source = EventsEmitter.Emit(model);

        // Assert
        await Assert.That(source).DoesNotContain("ProcessOrderFailed");
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static WorkflowModel CreateTestModel()
    {
        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState");
    }

    private static WorkflowModel CreateTestModelWithApproval(ApprovalModel approval)
    {
        return new WorkflowModel(
            WorkflowName: "process-order",
            PascalName: "ProcessOrder",
            Namespace: "TestNamespace",
            StepNames: ["ValidateOrder", "ProcessPayment", "SendConfirmation"],
            StateTypeName: "OrderState",
            ApprovalPoints: [approval]);
    }
}
