// =============================================================================
// <copyright file="FailureHandlerDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="FailureHandlerDefinition"/> record.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
///   <item><description>Create factory method produces valid definitions</description></item>
///   <item><description>Guard clauses validate inputs</description></item>
///   <item><description>Properties are correctly initialized</description></item>
/// </list>
/// </remarks>
[Property("Category", "Unit")]
public class FailureHandlerDefinitionTests
{
    // =============================================================================
    // A. Create Factory Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create returns a non-null definition.
    /// </summary>
    [Test]
    public async Task Create_WithValidInputs_ReturnsDefinition()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        // Assert
        await Assert.That(definition).IsNotNull();
    }

    /// <summary>
    /// Verifies that Create generates a unique HandlerId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueHandlerId()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
        };

        // Act
        var definition1 = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        var definition2 = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        // Assert
        await Assert.That(definition1.HandlerId).IsNotEqualTo(definition2.HandlerId);
    }

    /// <summary>
    /// Verifies that Create with null steps throws ArgumentNullException.
    /// </summary>
    [Test]
    public async Task Create_WithNullSteps_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            null!,
            isTerminal: true))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Scope Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create sets Workflow scope correctly.
    /// </summary>
    [Test]
    public async Task Create_WithWorkflowScope_SetsScopeCorrectly()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        // Assert
        await Assert.That(definition.Scope).IsEqualTo(FailureHandlerScope.Workflow);
    }

    /// <summary>
    /// Verifies that Create sets Step scope correctly.
    /// </summary>
    [Test]
    public async Task Create_WithStepScope_SetsScopeCorrectly()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(RefundStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Step,
            steps,
            isTerminal: false,
            triggerStepId: "step-123");

        // Assert
        await Assert.That(definition.Scope).IsEqualTo(FailureHandlerScope.Step);
    }

    // =============================================================================
    // C. TriggerStepId Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with Step scope stores TriggerStepId.
    /// </summary>
    [Test]
    public async Task Create_WithStepScope_StoresTriggerStepId()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(RefundStep)),
        };
        const string triggerStepId = "step-123";

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Step,
            steps,
            isTerminal: false,
            triggerStepId: triggerStepId);

        // Assert
        await Assert.That(definition.TriggerStepId).IsEqualTo(triggerStepId);
    }

    /// <summary>
    /// Verifies that Workflow scope has null TriggerStepId.
    /// </summary>
    [Test]
    public async Task Create_WithWorkflowScope_HasNullTriggerStepId()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        // Assert
        await Assert.That(definition.TriggerStepId).IsNull();
    }

    // =============================================================================
    // D. Steps Collection Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create stores steps correctly.
    /// </summary>
    [Test]
    public async Task Create_WithSteps_StoresStepsCorrectly()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
            StepDefinition.Create(typeof(NotifyAdminStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        // Assert
        await Assert.That(definition.Steps).HasCount().EqualTo(2);
        await Assert.That(definition.Steps[0].StepType).IsEqualTo(typeof(LogFailureStep));
        await Assert.That(definition.Steps[1].StepType).IsEqualTo(typeof(NotifyAdminStep));
    }

    // =============================================================================
    // E. IsTerminal Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create sets IsTerminal to true when specified.
    /// </summary>
    [Test]
    public async Task Create_WithIsTerminalTrue_SetsIsTerminal()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(LogFailureStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Workflow,
            steps,
            isTerminal: true);

        // Assert
        await Assert.That(definition.IsTerminal).IsTrue();
    }

    /// <summary>
    /// Verifies that Create sets IsTerminal to false when specified.
    /// </summary>
    [Test]
    public async Task Create_WithIsTerminalFalse_SetsIsTerminal()
    {
        // Arrange
        var steps = new List<StepDefinition>
        {
            StepDefinition.Create(typeof(RefundStep)),
        };

        // Act
        var definition = FailureHandlerDefinition.Create(
            FailureHandlerScope.Step,
            steps,
            isTerminal: false);

        // Assert
        await Assert.That(definition.IsTerminal).IsFalse();
    }
}
