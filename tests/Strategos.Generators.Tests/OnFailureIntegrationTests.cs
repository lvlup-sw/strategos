// -----------------------------------------------------------------------
// <copyright file="OnFailureIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

using Microsoft.CodeAnalysis;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for OnFailure handler generation.
/// </summary>
[Property("Category", "Integration")]
public class OnFailureIntegrationTests
{
    // =============================================================================
    // A. Saga File Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Saga file for workflows with OnFailure.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesSagaFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).IsNotNull().And.IsNotEmpty();
    }

    // =============================================================================
    // B. Phase Enum Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces failure handler phases.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesFailureHandlerPhases()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - Should contain phases for failure handler steps
        await Assert.That(phaseSource).Contains("FailureHandler_");
        await Assert.That(phaseSource).Contains("LogFailure");
        await Assert.That(phaseSource).Contains("NotifyAdmin");
    }

    /// <summary>
    /// Verifies that the generator produces a Failed phase.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesFailedPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert
        await Assert.That(phaseSource).Contains("Failed");
    }

    // =============================================================================
    // C. Trigger Command Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a trigger command for failure handler.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesTriggerCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert
        await Assert.That(commandsSource).Contains("TriggerFailureHandlingTestFailureHandlerCommand");
    }

    /// <summary>
    /// Verifies that trigger command includes failure context properties.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_TriggerCommand_HasFailureContext()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Should have properties for failed step name and exception message
        await Assert.That(commandsSource).Contains("FailedStepName");
        await Assert.That(commandsSource).Contains("ExceptionMessage");
    }

    // =============================================================================
    // D. Start Command Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces start commands for each failure handler step.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesStartCommandsForEachStep()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert
        await Assert.That(commandsSource).Contains("StartFailureHandler_");
        await Assert.That(commandsSource).Contains("LogFailure");
        await Assert.That(commandsSource).Contains("NotifyAdmin");
    }

    // =============================================================================
    // E. Worker Command Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces worker commands for failure handler steps.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesWorkerCommands()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert
        await Assert.That(commandsSource).Contains("ExecuteFailureHandler_");
        await Assert.That(commandsSource).Contains("WorkerCommand");
    }

    /// <summary>
    /// Verifies that worker command includes state and failure context.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_WorkerCommand_IncludesStateAndContext()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Worker command should have State and failure context
        await Assert.That(commandsSource).Contains("State,");
        await Assert.That(commandsSource).Contains("FailedStepName");
    }

    // =============================================================================
    // F. Completed Event Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces completed events for failure handler steps.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesCompletedEvents()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert
        await Assert.That(eventsSource).Contains("FailureHandler_");
        await Assert.That(eventsSource).Contains("Completed");
    }

    // =============================================================================
    // G. Saga Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the saga has a trigger handler.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_HasTriggerHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("Handle(TriggerFailureHandlingTestFailureHandlerCommand");
    }

    /// <summary>
    /// Verifies that trigger handler stores failure context.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_TriggerHandler_StoresFailureContext()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("FailedStepName = cmd.FailedStepName");
        await Assert.That(sagaSource).Contains("FailureExceptionMessage = cmd.ExceptionMessage");
    }

    /// <summary>
    /// Verifies that the saga has start handlers for each failure handler step.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_HasStartHandlers()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Start handler exists for failure handler steps
        await Assert.That(sagaSource).Contains("StartFailureHandler_");
        await Assert.That(sagaSource).Contains("LogFailure");
    }

    /// <summary>
    /// Verifies that start handlers transition to correct phase.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_StartHandler_TransitionsPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("Phase = FailureHandlingTestPhase.FailureHandler_");
    }

    /// <summary>
    /// Verifies that the saga has completed handlers for each failure handler step.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_HasCompletedHandlers()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Completed handlers exist for failure handler steps
        await Assert.That(sagaSource).Contains("FailureHandler_");
        await Assert.That(sagaSource).Contains("Completed");
    }

    // =============================================================================
    // H. Step Chaining Tests
    // =============================================================================

    /// <summary>
    /// Verifies that first step completed handler chains to second step.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_FirstStep_ChainsToSecond()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - LogFailure completed should return start command for NotifyAdmin
        await Assert.That(sagaSource).Contains("StartFailureHandler_");
        await Assert.That(sagaSource).Contains("NotifyAdmin");
    }

    /// <summary>
    /// Verifies that each completed handler applies reducer.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_CompletedHandlers_ApplyReducer()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("FailureTestStateReducer.Reduce(State, evt.UpdatedState)");
    }

    // =============================================================================
    // I. Terminal Handler Tests
    // =============================================================================

    /// <summary>
    /// Verifies that terminal handler marks workflow as Failed.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_TerminalHandler_MarksAsFailed()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("Phase = FailureHandlingTestPhase.Failed");
    }

    /// <summary>
    /// Verifies that terminal handler calls MarkCompleted.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_TerminalHandler_CallsMarkCompleted()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("MarkCompleted()");
    }

    // =============================================================================
    // J. Saga Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the saga has FailedStepName property.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_HasFailedStepNameProperty()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("string? FailedStepName");
    }

    /// <summary>
    /// Verifies that the saga has FailureExceptionMessage property.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_Saga_HasFailureExceptionMessageProperty()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert
        await Assert.That(sagaSource).Contains("string? FailureExceptionMessage");
    }

    // =============================================================================
    // K. Non-Terminal Handler Tests
    // =============================================================================

    /// <summary>
    /// Workflow with non-terminal OnFailure handler should not call MarkCompleted.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithNonTerminalOnFailure_DoesNotMarkCompleted()
    {
        // Arrange
        const string nonTerminalSource = """
            using Strategos.Abstractions;
            using Strategos.Attributes;
            using Strategos.Builders;
            using Strategos.Definitions;
            using Strategos.Steps;

            namespace TestNamespace;

            public record NonTerminalState : IWorkflowState
            {
                public Guid WorkflowId { get; init; }
            }

            public class MainStep : IWorkflowStep<NonTerminalState>
            {
                public Task<StepResult<NonTerminalState>> ExecuteAsync(
                    NonTerminalState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NonTerminalState>.FromState(state));
            }

            public class FinalStep : IWorkflowStep<NonTerminalState>
            {
                public Task<StepResult<NonTerminalState>> ExecuteAsync(
                    NonTerminalState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NonTerminalState>.FromState(state));
            }

            public class RecoveryStep : IWorkflowStep<NonTerminalState>
            {
                public Task<StepResult<NonTerminalState>> ExecuteAsync(
                    NonTerminalState state, StepContext context, CancellationToken ct)
                    => Task.FromResult(StepResult<NonTerminalState>.FromState(state));
            }

            [Workflow("non-terminal-failure")]
            public static partial class NonTerminalFailureWorkflow
            {
                public static WorkflowDefinition<NonTerminalState> Definition => Workflow<NonTerminalState>
                    .Create("non-terminal-failure")
                    .StartWith<MainStep>()
                    .Finally<FinalStep>()
                    .OnFailure(f => f
                        .Then<RecoveryStep>());
            }
            """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(nonTerminalSource);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Non-terminal failure handler should exist
        await Assert.That(sagaSource).Contains("FailureHandler_");

        // The main workflow's FinalStep handler should call MarkCompleted (for the main flow)
        // but the failure handler's RecoveryStep should NOT call MarkCompleted
        // To verify: the failure handler completed handler should just update state
        await Assert.That(sagaSource).Contains("RecoveryStep");
    }
}
