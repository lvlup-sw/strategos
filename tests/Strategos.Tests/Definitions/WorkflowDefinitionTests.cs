// =============================================================================
// <copyright file="WorkflowDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="WorkflowDefinition{TState}"/>.
/// </summary>
[Property("Category", "Unit")]
public class WorkflowDefinitionTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with a valid name returns an empty definition.
    /// </summary>
    [Test]
    public async Task Create_WithName_ReturnsEmptyDefinition()
    {
        // Arrange
        const string workflowName = "TestWorkflow";

        // Act
        var definition = WorkflowDefinition<TestWorkflowState>.Create(workflowName);

        // Assert
        await Assert.That(definition.Name).IsEqualTo(workflowName);
        await Assert.That(definition.Steps).IsEmpty();
        await Assert.That(definition.Transitions).IsEmpty();
        await Assert.That(definition.EntryStep).IsNull();
        await Assert.That(definition.TerminalStep).IsNull();
    }

    /// <summary>
    /// Verifies that Create throws for null name.
    /// </summary>
    [Test]
    public async Task Create_WithNullName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => WorkflowDefinition<TestWorkflowState>.Create(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for empty name.
    /// </summary>
    [Test]
    public async Task Create_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => WorkflowDefinition<TestWorkflowState>.Create(string.Empty))
            .Throws<ArgumentException>();
    }

    /// <summary>
    /// Verifies that Create throws for whitespace-only name.
    /// </summary>
    [Test]
    public async Task Create_WithWhitespaceName_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => WorkflowDefinition<TestWorkflowState>.Create("   "))
            .Throws<ArgumentException>();
    }

    // =============================================================================
    // B. WithStep Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithStep appends a step and preserves immutability.
    /// </summary>
    [Test]
    public async Task WithStep_AppendsStep_PreservesImmutability()
    {
        // Arrange
        var original = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var step = StepDefinition.Create(typeof(TestValidateStep));

        // Act
        var updated = original.WithStep(step);

        // Assert - Original is unchanged
        await Assert.That(original.Steps).IsEmpty();

        // Assert - Updated has the step
        await Assert.That(updated.Steps).HasCount().EqualTo(1);
        await Assert.That(updated.Steps[0]).IsEqualTo(step);
    }

    /// <summary>
    /// Verifies that WithStep can add multiple steps in sequence.
    /// </summary>
    [Test]
    public async Task WithStep_AddingMultipleSteps_AppendsInOrder()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var step1 = StepDefinition.Create(typeof(TestValidateStep));
        var step2 = StepDefinition.Create(typeof(TestProcessStep));
        var step3 = StepDefinition.Create(typeof(TestCompleteStep));

        // Act
        var result = definition
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3);

        // Assert
        await Assert.That(result.Steps).HasCount().EqualTo(3);
        await Assert.That(result.Steps[0]).IsEqualTo(step1);
        await Assert.That(result.Steps[1]).IsEqualTo(step2);
        await Assert.That(result.Steps[2]).IsEqualTo(step3);
    }

    /// <summary>
    /// Verifies that WithStep throws for null step.
    /// </summary>
    [Test]
    public async Task WithStep_WithNullStep_ThrowsArgumentNullException()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");

        // Act & Assert
        await Assert.That(() => definition.WithStep(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // C. Entry and Terminal Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithEntryStep sets the entry step.
    /// </summary>
    [Test]
    public async Task WithEntryStep_SetsEntryStep()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var step = StepDefinition.Create(typeof(TestValidateStep));

        // Act
        var result = definition.WithEntryStep(step);

        // Assert
        await Assert.That(result.EntryStep).IsNotNull();
        await Assert.That(result.EntryStep).IsEqualTo(step);
    }

    /// <summary>
    /// Verifies that WithTerminalStep sets the terminal step.
    /// </summary>
    [Test]
    public async Task WithTerminalStep_SetsTerminalStep()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var step = StepDefinition.Create(typeof(TestCompleteStep)).AsTerminal();

        // Act
        var result = definition.WithTerminalStep(step);

        // Assert
        await Assert.That(result.TerminalStep).IsNotNull();
        await Assert.That(result.TerminalStep).IsEqualTo(step);
    }

    /// <summary>
    /// Verifies that WithTerminalStep marks the step as terminal.
    /// </summary>
    [Test]
    public async Task WithTerminalStep_AutomaticallyMarksStepAsTerminal()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var step = StepDefinition.Create(typeof(TestCompleteStep)); // Not terminal initially

        // Act
        var result = definition.WithTerminalStep(step);

        // Assert
        await Assert.That(result.TerminalStep!.IsTerminal).IsTrue();
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WorkflowDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task WorkflowDefinition_IsImmutableRecord()
    {
        // Arrange
        var original = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var step = StepDefinition.Create(typeof(TestValidateStep));

        // Act - Use record with syntax
        var modified = original with { EntryStep = step };

        // Assert
        await Assert.That(original.EntryStep).IsNull();
        await Assert.That(modified.EntryStep).IsEqualTo(step);
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that Name is preserved through modifications.
    /// </summary>
    [Test]
    public async Task WithStep_PreservesName()
    {
        // Arrange
        const string workflowName = "OriginalName";
        var definition = WorkflowDefinition<TestWorkflowState>.Create(workflowName);
        var step = StepDefinition.Create(typeof(TestValidateStep));

        // Act
        var result = definition.WithStep(step);

        // Assert
        await Assert.That(result.Name).IsEqualTo(workflowName);
    }

    // =============================================================================
    // E. Loop Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Loops defaults to empty collection.
    /// </summary>
    [Test]
    public async Task Loops_DefaultsToEmptyCollection()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");

        // Assert
        await Assert.That(definition.Loops).IsEmpty();
    }

    /// <summary>
    /// Verifies that WithLoop adds a loop definition.
    /// </summary>
    [Test]
    public async Task WithLoop_AddsLoopDefinition()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestValidateStep)) };
        var loop = LoopDefinition.Create("Refinement", "step-1", 5, bodySteps);

        // Act
        var updated = definition.WithLoop(loop);

        // Assert
        await Assert.That(updated.Loops).HasCount().EqualTo(1);
        await Assert.That(updated.Loops[0].LoopName).IsEqualTo("Refinement");
    }

    /// <summary>
    /// Verifies that WithLoop preserves original definition.
    /// </summary>
    [Test]
    public async Task WithLoop_PreservesOriginal()
    {
        // Arrange
        var original = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestValidateStep)) };
        var loop = LoopDefinition.Create("Refinement", "step-1", 5, bodySteps);

        // Act
        var updated = original.WithLoop(loop);

        // Assert
        await Assert.That(original.Loops).IsEmpty();
        await Assert.That(updated.Loops).HasCount().EqualTo(1);
    }

    /// <summary>
    /// Verifies that WithLoop can add multiple loops.
    /// </summary>
    [Test]
    public async Task WithLoop_CanAddMultipleLoops()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestValidateStep)) };
        var loop1 = LoopDefinition.Create("Refinement", "step-1", 5, bodySteps);
        var loop2 = LoopDefinition.Create("Validation", "step-2", 3, bodySteps);

        // Act
        var updated = definition
            .WithLoop(loop1)
            .WithLoop(loop2);

        // Assert
        await Assert.That(updated.Loops).HasCount().EqualTo(2);
        await Assert.That(updated.Loops[0].LoopName).IsEqualTo("Refinement");
        await Assert.That(updated.Loops[1].LoopName).IsEqualTo("Validation");
    }

    /// <summary>
    /// Verifies that WithLoop throws for null loop.
    /// </summary>
    [Test]
    public async Task WithLoop_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");

        // Act & Assert
        await Assert.That(() => definition.WithLoop(null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that WithLoops sets the loops collection.
    /// </summary>
    [Test]
    public async Task WithLoops_SetsLoopsCollection()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");
        var bodySteps = new List<StepDefinition> { StepDefinition.Create(typeof(TestValidateStep)) };
        var loops = new List<LoopDefinition>
        {
            LoopDefinition.Create("Loop1", "step-1", 5, bodySteps),
            LoopDefinition.Create("Loop2", "step-2", 3, bodySteps),
        };

        // Act
        var updated = definition.WithLoops(loops);

        // Assert
        await Assert.That(updated.Loops).HasCount().EqualTo(2);
    }

    /// <summary>
    /// Verifies that WithLoops throws for null collection.
    /// </summary>
    [Test]
    public async Task WithLoops_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var definition = WorkflowDefinition<TestWorkflowState>.Create("TestWorkflow");

        // Act & Assert
        await Assert.That(() => definition.WithLoops(null!))
            .Throws<ArgumentNullException>();
    }
}

/// <summary>
/// Test step class for validation step.
/// </summary>
internal sealed class TestValidateStep
{
}

/// <summary>
/// Test step class for processing step.
/// </summary>
internal sealed class TestProcessStep
{
}

/// <summary>
/// Test step class for completion step.
/// </summary>
internal sealed class TestCompleteStep
{
}
