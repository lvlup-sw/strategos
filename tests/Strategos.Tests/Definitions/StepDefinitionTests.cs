// =============================================================================
// <copyright file="StepDefinitionTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Definitions;

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="StepDefinition"/>.
/// </summary>
[Property("Category", "Unit")]
public class StepDefinitionTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create captures the step type name.
    /// </summary>
    [Test]
    public async Task Create_WithStepType_CapturesTypeName()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition.StepTypeName).IsEqualTo("TestValidateOrderStep");
    }

    /// <summary>
    /// Verifies that Create derives the phase name from the type name.
    /// </summary>
    [Test]
    public async Task Create_WithStepType_DerivesPhaseName()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert - Should strip "Step" suffix and convert to phase name
        await Assert.That(definition.StepName).IsEqualTo("TestValidateOrder");
    }

    /// <summary>
    /// Verifies that Create uses custom name when provided.
    /// </summary>
    [Test]
    public async Task Create_WithCustomName_UsesCustomName()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);
        const string customName = "validate-order";

        // Act
        var definition = StepDefinition.Create(stepType, customName);

        // Assert
        await Assert.That(definition.StepName).IsEqualTo("validate-order");
    }

    /// <summary>
    /// Verifies that Create throws for null step type.
    /// </summary>
    [Test]
    public async Task Create_WithNullType_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => StepDefinition.Create(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Property Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepDefinition generates a unique StepId.
    /// </summary>
    [Test]
    public async Task Create_GeneratesUniqueStepId()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition1 = StepDefinition.Create(stepType);
        var definition2 = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition1.StepId).IsNotNull();
        await Assert.That(definition1.StepId).IsNotEqualTo(definition2.StepId);
    }

    /// <summary>
    /// Verifies that StepType property returns the provided type.
    /// </summary>
    [Test]
    public async Task StepType_ReturnsProvidedType()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition.StepType).IsEqualTo(stepType);
    }

    /// <summary>
    /// Verifies that IsTerminal defaults to false.
    /// </summary>
    [Test]
    public async Task IsTerminal_DefaultsToFalse()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition.IsTerminal).IsFalse();
    }

    // =============================================================================
    // C. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that StepDefinition is an immutable record.
    /// </summary>
    [Test]
    public async Task StepDefinition_IsImmutableRecord()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);
        var original = StepDefinition.Create(stepType);

        // Act - Use record with syntax
        var modified = original with { IsTerminal = true };

        // Assert
        await Assert.That(original.IsTerminal).IsFalse();
        await Assert.That(modified.IsTerminal).IsTrue();
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that AsTerminal creates a new instance with IsTerminal set to true.
    /// </summary>
    [Test]
    public async Task AsTerminal_ReturnsNewInstanceWithIsTerminalTrue()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);
        var original = StepDefinition.Create(stepType);

        // Act
        var terminal = original.AsTerminal();

        // Assert
        await Assert.That(original.IsTerminal).IsFalse();
        await Assert.That(terminal.IsTerminal).IsTrue();
        await Assert.That(terminal.StepId).IsEqualTo(original.StepId);
    }

    // =============================================================================
    // D. Configuration Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Configuration defaults to null.
    /// </summary>
    [Test]
    public async Task Configuration_DefaultsToNull()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition.Configuration).IsNull();
    }

    /// <summary>
    /// Verifies that WithConfiguration sets the configuration.
    /// </summary>
    [Test]
    public async Task WithConfiguration_SetsConfiguration()
    {
        // Arrange
        var definition = StepDefinition.Create(typeof(TestValidateOrderStep));
        var config = StepConfigurationDefinition.WithConfidence(0.85);

        // Act
        var updated = definition.WithConfiguration(config);

        // Assert
        await Assert.That(updated.Configuration).IsNotNull();
        await Assert.That(updated.Configuration!.ConfidenceThreshold).IsEqualTo(0.85);
    }

    /// <summary>
    /// Verifies that WithConfiguration preserves original instance.
    /// </summary>
    [Test]
    public async Task WithConfiguration_PreservesOriginal()
    {
        // Arrange
        var original = StepDefinition.Create(typeof(TestValidateOrderStep));
        var config = StepConfigurationDefinition.WithConfidence(0.85);

        // Act
        var updated = original.WithConfiguration(config);

        // Assert
        await Assert.That(original.Configuration).IsNull();
        await Assert.That(updated.Configuration).IsNotNull();
    }

    /// <summary>
    /// Verifies that WithConfiguration throws for null configuration.
    /// </summary>
    [Test]
    public async Task WithConfiguration_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var definition = StepDefinition.Create(typeof(TestValidateOrderStep));

        // Act & Assert
        await Assert.That(() => definition.WithConfiguration(null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // E. Loop Body Step Tests
    // =============================================================================

    /// <summary>
    /// Verifies that IsLoopBodyStep defaults to false.
    /// </summary>
    [Test]
    public async Task IsLoopBodyStep_DefaultsToFalse()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition.IsLoopBodyStep).IsFalse();
    }

    /// <summary>
    /// Verifies that ParentLoopId defaults to null.
    /// </summary>
    [Test]
    public async Task ParentLoopId_DefaultsToNull()
    {
        // Arrange
        var stepType = typeof(TestValidateOrderStep);

        // Act
        var definition = StepDefinition.Create(stepType);

        // Assert
        await Assert.That(definition.ParentLoopId).IsNull();
    }

    /// <summary>
    /// Verifies that AsLoopBodyStep sets both IsLoopBodyStep and ParentLoopId.
    /// </summary>
    [Test]
    public async Task AsLoopBodyStep_SetsLoopProperties()
    {
        // Arrange
        var definition = StepDefinition.Create(typeof(TestValidateOrderStep));
        const string loopId = "loop-123";

        // Act
        var updated = definition.AsLoopBodyStep(loopId);

        // Assert
        await Assert.That(updated.IsLoopBodyStep).IsTrue();
        await Assert.That(updated.ParentLoopId).IsEqualTo("loop-123");
    }

    /// <summary>
    /// Verifies that AsLoopBodyStep preserves original instance.
    /// </summary>
    [Test]
    public async Task AsLoopBodyStep_PreservesOriginal()
    {
        // Arrange
        var original = StepDefinition.Create(typeof(TestValidateOrderStep));

        // Act
        var updated = original.AsLoopBodyStep("loop-123");

        // Assert
        await Assert.That(original.IsLoopBodyStep).IsFalse();
        await Assert.That(original.ParentLoopId).IsNull();
        await Assert.That(updated.IsLoopBodyStep).IsTrue();
    }

    /// <summary>
    /// Verifies that AsLoopBodyStep throws for null loop ID.
    /// </summary>
    [Test]
    public async Task AsLoopBodyStep_WithNullLoopId_ThrowsArgumentNullException()
    {
        // Arrange
        var definition = StepDefinition.Create(typeof(TestValidateOrderStep));

        // Act & Assert
        await Assert.That(() => definition.AsLoopBodyStep(null!))
            .Throws<ArgumentNullException>();
    }
}

/// <summary>
/// Test step class for unit testing.
/// </summary>
internal sealed class TestValidateOrderStep
{
}
