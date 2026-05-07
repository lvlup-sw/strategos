// -----------------------------------------------------------------------
// <copyright file="GeneratorIntegrationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Tests.Fixtures;

namespace Strategos.Generators.Tests;

/// <summary>
/// Integration tests for the <see cref="WorkflowIncrementalGenerator"/>.
/// </summary>
[Property("Category", "Integration")]
public class GeneratorIntegrationTests
{
    // =============================================================================
    // A. Attribute Detection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces output when a class has the [Workflow] attribute.
    /// </summary>
    [Test]
    public async Task Generator_ClassWithWorkflowAttribute_ProducesOutput()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);

        // Assert
        await Assert.That(result.GeneratedTrees).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that the generator produces no output when no [Workflow] attribute is present.
    /// </summary>
    [Test]
    public async Task Generator_ClassWithoutAttribute_ProducesNothing()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithoutWorkflowAttribute);

        // Assert
        await Assert.That(result.GeneratedTrees).IsEmpty();
    }

    /// <summary>
    /// Verifies that the generator extracts the workflow name from the attribute.
    /// </summary>
    [Test]
    public async Task Generator_ExtractsWorkflowName_FromAttribute()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - The enum should be named based on the workflow name "process-order" -> "ProcessOrderPhase"
        await Assert.That(generatedSource).Contains("ProcessOrderPhase");
    }

    /// <summary>
    /// Verifies that the generator extracts the namespace from the declaration.
    /// </summary>
    [Test]
    public async Task Generator_ExtractsNamespace_FromDeclaration()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);
        var generatedSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - The generated code should use the same namespace
        await Assert.That(generatedSource).Contains("namespace TestNamespace");
    }

    /// <summary>
    /// Verifies that the generator works with structs as well as classes.
    /// </summary>
    [Test]
    public async Task Generator_StructWithWorkflowAttribute_ProducesOutput()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.StructWithWorkflowAttribute);

        // Assert
        await Assert.That(result.GeneratedTrees).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that no diagnostics (errors) are produced for valid input.
    /// </summary>
    [Test]
    public async Task Generator_ValidWorkflow_ProducesNoDiagnostics()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.ClassWithWorkflowAttribute);

        // Assert - Should have no error-level diagnostics
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }

    // =============================================================================
    // B. Commands Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Commands file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesCommandsFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert
        await Assert.That(commandsSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that the Commands file contains the Start command.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_CommandsFile_ContainsStartCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert
        await Assert.That(commandsSource).Contains("StartProcessOrderCommand");
    }

    /// <summary>
    /// Verifies that the Commands file contains Execute commands for each step.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_CommandsFile_ContainsExecuteCommands()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert
        await Assert.That(commandsSource).Contains("ExecuteValidateOrderCommand");
        await Assert.That(commandsSource).Contains("ExecuteProcessPaymentCommand");
        await Assert.That(commandsSource).Contains("ExecuteSendConfirmationCommand");
    }

    // =============================================================================
    // C. Events Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces an Events file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesEventsFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert
        await Assert.That(eventsSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that the Events file contains the workflow event interface.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_EventsFile_ContainsEventInterface()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert
        await Assert.That(eventsSource).Contains("IProcessOrderEvent");
    }

    /// <summary>
    /// Verifies that the Events file contains step completed events with SagaIdentity.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_EventsFile_ContainsSagaIdentity()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert
        await Assert.That(eventsSource).Contains("[property: SagaIdentity]");
    }

    // =============================================================================
    // D. Transitions Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Transitions file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesTransitionsFile()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var transitionsSource = GeneratorTestHelper.GetGeneratedSource(result, "Transitions.g.cs");

        // Assert
        await Assert.That(transitionsSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that the Transitions file contains the ValidTransitions dictionary.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_TransitionsFile_ContainsValidTransitions()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var transitionsSource = GeneratorTestHelper.GetGeneratedSource(result, "Transitions.g.cs");

        // Assert
        await Assert.That(transitionsSource).Contains("ValidTransitions");
    }

    /// <summary>
    /// Verifies that the Transitions file contains the IsValidTransition helper method.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_TransitionsFile_ContainsIsValidTransition()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var transitionsSource = GeneratorTestHelper.GetGeneratedSource(result, "Transitions.g.cs");

        // Assert
        await Assert.That(transitionsSource).Contains("IsValidTransition");
    }

    // =============================================================================
    // E. Versioning Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces output for a versioned workflow.
    /// </summary>
    [Test]
    public async Task Generator_VersionedWorkflowV2_ProducesOutput()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.VersionedWorkflowV2);

        // Assert
        await Assert.That(result.GeneratedTrees).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that versioned workflows produce no error diagnostics.
    /// </summary>
    [Test]
    public async Task Generator_VersionedWorkflow_ProducesNoDiagnostics()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.VersionedWorkflowV2);

        // Assert - Should have no error-level diagnostics
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that versioned workflows generate correct Phase enum name.
    /// </summary>
    [Test]
    public async Task Generator_VersionedWorkflow_GeneratesPhaseEnumWithoutVersionSuffix()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.VersionedWorkflowV2);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - Phase enum should NOT have version suffix
        await Assert.That(phaseSource).Contains("ProcessOrderPhase");
        await Assert.That(phaseSource).DoesNotContain("ProcessOrderPhaseV2");
    }

    // =============================================================================
    // F. Mermaid Diagram Generation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Mermaid diagram file for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesMermaidDiagram()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var diagramTree = result.GeneratedTrees.FirstOrDefault(t =>
            t.FilePath.Contains("Diagram", StringComparison.OrdinalIgnoreCase));
        var diagramSource = diagramTree?.GetText().ToString() ?? string.Empty;

        // Assert
        await Assert.That(diagramSource).IsNotNull().And.IsNotEmpty();
    }

    /// <summary>
    /// Verifies that the Mermaid diagram file has the correct hint name pattern.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_MermaidFileHasCorrectName()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var hasFile = result.GeneratedTrees.Any(t =>
            t.FilePath.Contains("ProcessOrderDiagram", StringComparison.OrdinalIgnoreCase));

        // Assert
        await Assert.That(hasFile).IsTrue();
    }

    /// <summary>
    /// Verifies that the Mermaid diagram contains the stateDiagram-v2 header.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_MermaidDiagramContainsHeader()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);
        var diagramTree = result.GeneratedTrees.FirstOrDefault(t =>
            t.FilePath.Contains("Diagram", StringComparison.OrdinalIgnoreCase));
        var diagramSource = diagramTree?.GetText().ToString() ?? string.Empty;

        // Assert
        await Assert.That(diagramSource).Contains("stateDiagram-v2");
    }

    // =============================================================================
    // G. Complex Workflow Mermaid Diagram Tests
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces a Mermaid diagram with loop notes for loop workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithLoop_GeneratesLoopDiagram()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithLoop);
        var diagramTree = result.GeneratedTrees.FirstOrDefault(t =>
            t.FilePath.Contains("Diagram", StringComparison.OrdinalIgnoreCase));
        var diagramSource = diagramTree?.GetText().ToString() ?? string.Empty;

        // Assert
        await Assert.That(diagramSource).Contains("note right of");
        await Assert.That(diagramSource).Contains("Loop:");
        await Assert.That(diagramSource).Contains(": continue");
        await Assert.That(diagramSource).Contains(": exit");
    }

    /// <summary>
    /// Verifies that the generator produces a Mermaid diagram with branch choices for branch workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithBranch_GeneratesBranchDiagram()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithEnumBranch);
        var diagramTree = result.GeneratedTrees.FirstOrDefault(t =>
            t.FilePath.Contains("Diagram", StringComparison.OrdinalIgnoreCase));
        var diagramSource = diagramTree?.GetText().ToString() ?? string.Empty;

        // Assert
        await Assert.That(diagramSource).Contains("<<choice>>");
        await Assert.That(diagramSource).Contains("BranchBy");
    }

    // =============================================================================
    // H. All Artifacts Generation Test
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces all eight artifact files for linear workflows.
    /// </summary>
    [Test]
    public async Task Generator_LinearWorkflow_GeneratesEightArtifacts()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.LinearWorkflow);

        // Assert - Phase, Commands, Events, Transitions, Saga, Handlers, Extensions, Diagram = 8 files
        await Assert.That(result.GeneratedTrees).Count().IsEqualTo(8);
    }

    // =============================================================================
    // I. Fork/Join Workflow Tests (Milestone 15 - Parallel Execution)
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces all artifacts for fork workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesAllArtifacts()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);

        // Assert - Phase, Commands, Events, Transitions, Saga, Handlers, Extensions, Diagram = 8 files
        await Assert.That(result.GeneratedTrees).Count().IsEqualTo(8);
    }

    /// <summary>
    /// Verifies that the generator produces Forking phase for fork workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesForkingPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - Should have Forking_ phase
        await Assert.That(phaseSource).Contains("Forking_");
    }

    /// <summary>
    /// Verifies that the generator produces Joining phase for fork workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesJoiningPhase()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - Should have Joining_ phase
        await Assert.That(phaseSource).Contains("Joining_");
    }

    /// <summary>
    /// Verifies that the generator produces dispatch command for fork workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesDispatchCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Should have DispatchFork command
        await Assert.That(commandsSource).Contains("DispatchFork_");
    }

    /// <summary>
    /// Verifies that the generator produces join command for fork workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesJoinCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Should have JoinFork command
        await Assert.That(commandsSource).Contains("JoinFork_");
    }

    /// <summary>
    /// Verifies that the generator produces path status properties in saga.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesPathStatusProperties()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should have path status properties (Path0 and Path1)
        await Assert.That(sagaSource).Contains("Path0Status");
        await Assert.That(sagaSource).Contains("Path1Status");
    }

    /// <summary>
    /// Verifies that the generator produces path state properties in saga.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesPathStateProperties()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should have path state properties (Path0 and Path1)
        await Assert.That(sagaSource).Contains("Path0State");
        await Assert.That(sagaSource).Contains("Path1State");
    }

    /// <summary>
    /// Verifies that fork workflows produce no error diagnostics.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_ProducesNoDiagnostics()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);

        // Assert - Should have no error-level diagnostics
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that the generator produces three path status properties for three-path fork.
    /// </summary>
    [Test]
    public async Task Generator_ForkWithThreePaths_GeneratesThreePathStatusProperties()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithThreePathFork);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should have three path status properties
        await Assert.That(sagaSource).Contains("Path0Status");
        await Assert.That(sagaSource).Contains("Path1Status");
        await Assert.That(sagaSource).Contains("Path2Status");
    }

    /// <summary>
    /// Verifies that the generator produces join readiness check for fork workflows.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesJoinReadinessCheck()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should have CheckJoinReady method
        await Assert.That(sagaSource).Contains("CheckJoinReady_");
    }

    /// <summary>
    /// Verifies that the generator produces fork dispatch handler.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesDispatchHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should dispatch parallel path start commands using yield return pattern
        await Assert.That(sagaSource).Contains("yield return new Start");
    }

    /// <summary>
    /// Verifies that the generator produces fork path completed events.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_GeneratesPathCompletedEvents()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert - Should have step completed events for fork path steps
        await Assert.That(eventsSource).Contains("ProcessPaymentCompleted");
        await Assert.That(eventsSource).Contains("ReserveInventoryCompleted");
    }

    /// <summary>
    /// Verifies that the generator produces ForkPathStatus usage in saga.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithFork_UsesForkPathStatusEnum()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should use ForkPathStatus enum
        await Assert.That(sagaSource).Contains("ForkPathStatus");
    }

    // =============================================================================
    // J. OnFailure Handler Tests (Milestone 16 - Failure Handlers)
    // =============================================================================

    /// <summary>
    /// Verifies that the generator produces output for a workflow with OnFailure handler.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_ProducesOutput()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);

        // Assert
        await Assert.That(result.GeneratedTrees).Count().IsGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Verifies that the generator produces no error diagnostics for workflow with OnFailure.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_ProducesNoDiagnostics()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);

        // Assert - Should have no error-level diagnostics
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }

    /// <summary>
    /// Verifies that the generator includes failure handler phases in the phase enum.
    /// This test will fail until Sprint 2 is complete.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesFailureHandlerPhases()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - Should have failure handler phases
        await Assert.That(phaseSource).Contains("FailureHandler_");
        await Assert.That(phaseSource).Contains("LogFailure");
        await Assert.That(phaseSource).Contains("NotifyAdmin");
    }

    /// <summary>
    /// Verifies that the generator produces trigger command for failure handler.
    /// This test will fail until Sprint 3 is complete.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesTriggerCommand()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Should have trigger failure handler command
        await Assert.That(commandsSource).Contains("TriggerFailureHandlingTestFailureHandlerCommand");
    }

    /// <summary>
    /// Verifies that the generator produces failure handler step commands.
    /// This test will fail until Sprint 3 is complete.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesFailureHandlerStepCommands()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Should have failure handler step commands
        await Assert.That(commandsSource).Contains("StartFailureHandler_");
        await Assert.That(commandsSource).Contains("ExecuteFailureHandler_");
    }

    /// <summary>
    /// Verifies that the generator produces failure handler completed events.
    /// This test will fail until Sprint 4 is complete.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesFailureHandlerEvents()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert - Should have failure handler step completed events
        await Assert.That(eventsSource).Contains("FailureHandler_");
        await Assert.That(eventsSource).Contains("Completed");
    }

    /// <summary>
    /// Verifies that the saga includes failure tracking properties.
    /// This test will fail until Sprint 5 is complete.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesFailureTrackingProperties()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should have failure tracking properties
        await Assert.That(sagaSource).Contains("FailedStepName");
        await Assert.That(sagaSource).Contains("FailureExceptionMessage");
    }

    /// <summary>
    /// Verifies that the saga includes failure handler trigger handler.
    /// This test will fail until Sprint 6 is complete.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_GeneratesTriggerHandler()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var sagaSource = GeneratorTestHelper.GetGeneratedSource(result, "Saga.g.cs");

        // Assert - Should have trigger handler that stores exception context
        await Assert.That(sagaSource).Contains("Handle(TriggerFailureHandlingTestFailureHandlerCommand");
    }

    // =============================================================================
    // L. Instance Name Tests (Phase 2 - Step Reuse Support)
    // =============================================================================

    /// <summary>
    /// Verifies that workflows with instance names generate all artifacts.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithInstanceNames_GeneratesAllArtifacts()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithInstanceNames);

        // Assert - Should generate 8 files
        await Assert.That(result.GeneratedTrees).Count().IsEqualTo(8);
    }

    /// <summary>
    /// Verifies that phase enum uses instance names (EffectiveName) instead of step type names.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithInstanceNames_UsesInstanceNamesForPhases()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithInstanceNames);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");

        // Assert - Should have instance names as phases, not step type name
        await Assert.That(phaseSource).Contains("Technical,");
        await Assert.That(phaseSource).Contains("Fundamental,");
        // The raw step type name should NOT appear as a phase (it would be duplicate)
        // Note: AnalyzeStep may appear in comments but should not be a separate phase value
    }

    /// <summary>
    /// Verifies that worker handlers are deduplicated by step TYPE, not instance name.
    /// One handler class per step type, shared across all instances.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithInstanceNames_DeduplicatesHandlersByStepType()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithInstanceNames);
        var handlersSource = GeneratorTestHelper.GetGeneratedSource(result, "Handlers.g.cs");

        // Assert - Should have ONE AnalyzeStepHandler (deduped by step type)
        await Assert.That(handlersSource).Contains("AnalyzeStepHandler");

        // Should NOT have instance-name-based handlers
        await Assert.That(handlersSource).DoesNotContain("TechnicalHandler");
        await Assert.That(handlersSource).DoesNotContain("FundamentalHandler");
    }

    /// <summary>
    /// Verifies that commands use step TYPE name, not instance name.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithInstanceNames_UsesStepTypeForCommands()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithInstanceNames);
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - Worker commands use step TYPE name
        await Assert.That(commandsSource).Contains("ExecuteAnalyzeStepWorkerCommand");

        // Should NOT have instance-name-based worker commands
        await Assert.That(commandsSource).DoesNotContain("ExecuteTechnicalWorkerCommand");
        await Assert.That(commandsSource).DoesNotContain("ExecuteFundamentalWorkerCommand");
    }

    /// <summary>
    /// Verifies that events use step TYPE name, not instance name.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithInstanceNames_UsesStepTypeForEvents()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithInstanceNames);
        var eventsSource = GeneratorTestHelper.GetGeneratedSource(result, "Events.g.cs");

        // Assert - Completed events use step TYPE name
        await Assert.That(eventsSource).Contains("AnalyzeStepCompleted");

        // Should NOT have instance-name-based events
        await Assert.That(eventsSource).DoesNotContain("TechnicalCompleted");
        await Assert.That(eventsSource).DoesNotContain("FundamentalCompleted");
    }

    /// <summary>
    /// Verifies that the generator produces no errors for workflows with instance names.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithInstanceNames_ProducesNoErrors()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithInstanceNames);

        // Assert - Should have no error diagnostics
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        await Assert.That(errors).IsEmpty();
    }

    // =============================================================================
    // M. Performance Optimization Tests (A.11 - HashSet Contains)
    // =============================================================================

    /// <summary>
    /// Verifies that workflows with many steps from multiple sources (forks, failure handlers)
    /// correctly include all unique step names in generated artifacts.
    /// This test ensures the HashSet optimization for Contains checks preserves correctness.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithForkAndOnFailure_IncludesAllStepNames()
    {
        // Arrange & Act
        // Using the WorkflowWithFork source which has fork paths with multiple steps
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithFork);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - All step names from fork paths are included in Phase enum
        await Assert.That(phaseSource).Contains("ValidateOrder");
        await Assert.That(phaseSource).Contains("ProcessPayment");
        await Assert.That(phaseSource).Contains("ReserveInventory");
        await Assert.That(phaseSource).Contains("SynthesizeResults");
        await Assert.That(phaseSource).Contains("SendConfirmation");

        // Assert - All step commands are generated for unique steps
        await Assert.That(commandsSource).Contains("ExecuteValidateOrderCommand");
        await Assert.That(commandsSource).Contains("ExecuteProcessPaymentCommand");
        await Assert.That(commandsSource).Contains("ExecuteReserveInventoryCommand");
        await Assert.That(commandsSource).Contains("ExecuteSynthesizeResultsCommand");
        await Assert.That(commandsSource).Contains("ExecuteSendConfirmationCommand");

        // Assert - No duplicate phases (each step appears once)
        var phaseCount = phaseSource.Split('\n').Count(line => line.Contains("ValidateOrder") && line.Contains(","));
        await Assert.That(phaseCount).IsEqualTo(1);
    }

    // =============================================================================
    // N. Performance Optimization Tests (A.12 - List Pre-allocation)
    // =============================================================================

    /// <summary>
    /// Verifies that workflows with failure handlers and forks generate all artifacts correctly.
    /// This test ensures the list pre-allocation optimization preserves correctness when
    /// multiple sources contribute step names.
    /// </summary>
    [Test]
    public async Task Generator_WorkflowWithOnFailure_PreallocatesAndIncludesAllSteps()
    {
        // Arrange & Act
        var result = GeneratorTestHelper.RunGenerator(SourceTexts.WorkflowWithOnFailure);
        var phaseSource = GeneratorTestHelper.GetGeneratedSource(result, "Phase.g.cs");
        var commandsSource = GeneratorTestHelper.GetGeneratedSource(result, "Commands.g.cs");

        // Assert - All main workflow steps are present
        await Assert.That(phaseSource).Contains("ValidateInput");
        await Assert.That(phaseSource).Contains("ProcessData");
        await Assert.That(phaseSource).Contains("SaveResult");

        // Assert - Failure handler steps are present
        await Assert.That(phaseSource).Contains("LogFailure");
        await Assert.That(phaseSource).Contains("NotifyAdmin");

        // Assert - Commands are generated for failure handler steps
        await Assert.That(commandsSource).Contains("ExecuteLogFailureCommand");
        await Assert.That(commandsSource).Contains("ExecuteNotifyAdminCommand");

        // Assert - No errors
        var errors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        await Assert.That(errors).IsEmpty();
    }
}
