// =============================================================================
// <copyright file="CompensationConfigurationTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Tests.Definitions;

/// <summary>
/// Unit tests for <see cref="CompensationConfiguration"/>.
/// </summary>
[Property("Category", "Unit")]
public class CompensationConfigurationTests
{
    // =============================================================================
    // A. Factory Method Tests
    // =============================================================================

    /// <summary>
    /// Verifies that Create with a type captures the compensation step type.
    /// </summary>
    [Test]
    public async Task Create_WithStepType_CapturesType()
    {
        // Act
        var config = CompensationConfiguration.Create(typeof(TestCompensationStep));

        // Assert
        await Assert.That(config.CompensationStepType).IsEqualTo(typeof(TestCompensationStep));
    }

    /// <summary>
    /// Verifies that the generic Create method captures the type.
    /// </summary>
    [Test]
    public async Task Create_Generic_CapturesGenericType()
    {
        // Act
        var config = CompensationConfiguration.Create<TestCompensationStep>();

        // Assert
        await Assert.That(config.CompensationStepType).IsEqualTo(typeof(TestCompensationStep));
    }

    /// <summary>The typed factory retains a language-neutral inverse action identity.</summary>
    [Test]
    public async Task Create_WithInverseAction_CapturesStructuralIdentity()
    {
        var inverse = new WorkflowActionReference("orders", "Order", "refund");

        var genericConfig = CompensationConfiguration.Create<TestCompensationStep>(inverse);
        var runtimeTypeConfig = CompensationConfiguration.Create(
            typeof(TestCompensationStep),
            inverse);

        await Assert.That(genericConfig.InverseAction).IsEqualTo(inverse);
        await Assert.That(genericConfig.CompensationStepType).IsEqualTo(typeof(TestCompensationStep));
        await Assert.That(runtimeTypeConfig).IsEqualTo(genericConfig);
    }

    /// <summary>The typed factory rejects a missing inverse identity at construction.</summary>
    [Test]
    public async Task Create_WithNullInverseAction_ThrowsArgumentNullException()
    {
        await Assert.That(() =>
                CompensationConfiguration.Create<TestCompensationStep>(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() =>
                CompensationConfiguration.Create(typeof(TestCompensationStep), null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that Create throws for null step type.
    /// </summary>
    [Test]
    public async Task Create_WithNullType_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => CompensationConfiguration.Create(null!))
            .Throws<ArgumentNullException>();
        await Assert.That(() => CompensationConfiguration.Create(
                null!,
                new WorkflowActionReference("orders", "Order", "refund")))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // B. Default Value Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RequiredOnFailure defaults to true.
    /// </summary>
    [Test]
    public async Task Create_RequiredOnFailure_DefaultsToTrue()
    {
        // Act
        var config = CompensationConfiguration.Create<TestCompensationStep>();

        // Assert
        await Assert.That(config.RequiredOnFailure).IsTrue();
    }

    /// <summary>
    /// Verifies that Timeout defaults to null.
    /// </summary>
    [Test]
    public async Task Create_Timeout_DefaultsToNull()
    {
        // Act
        var config = CompensationConfiguration.Create<TestCompensationStep>();

        // Assert
        await Assert.That(config.Timeout).IsNull();
    }

    // =============================================================================
    // C. WithTimeout Tests
    // =============================================================================

    /// <summary>
    /// Verifies that WithTimeout sets the timeout value.
    /// </summary>
    [Test]
    public async Task WithTimeout_SetsTimeout()
    {
        // Arrange
        var config = CompensationConfiguration.Create<TestCompensationStep>();
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        var updated = config.WithTimeout(timeout);

        // Assert
        await Assert.That(updated.Timeout).IsEqualTo(TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Verifies that WithTimeout preserves original instance.
    /// </summary>
    [Test]
    public async Task WithTimeout_PreservesOriginal()
    {
        // Arrange
        var original = CompensationConfiguration.Create<TestCompensationStep>();

        // Act
        var updated = original.WithTimeout(TimeSpan.FromMinutes(5));

        // Assert
        await Assert.That(original.Timeout).IsNull();
        await Assert.That(updated.Timeout).IsNotNull();
    }

    // =============================================================================
    // D. Immutability Tests
    // =============================================================================

    /// <summary>
    /// Verifies that CompensationConfiguration is an immutable record.
    /// </summary>
    [Test]
    public async Task CompensationConfiguration_IsImmutableRecord()
    {
        // Arrange
        var original = CompensationConfiguration.Create<TestCompensationStep>();

        // Act - Use record with syntax
        var modified = original with { RequiredOnFailure = false };

        // Assert
        await Assert.That(original.RequiredOnFailure).IsTrue();
        await Assert.That(modified.RequiredOnFailure).IsFalse();
        await Assert.That(original).IsNotEqualTo(modified);
    }

    /// <summary>
    /// Verifies that equal configurations are considered equal.
    /// </summary>
    [Test]
    public async Task Equality_SameValues_AreEqual()
    {
        // Arrange
        var config1 = CompensationConfiguration.Create<TestCompensationStep>();
        var config2 = CompensationConfiguration.Create<TestCompensationStep>();

        // Assert - Same type, same defaults
        await Assert.That(config1.CompensationStepType).IsEqualTo(config2.CompensationStepType);
        await Assert.That(config1.RequiredOnFailure).IsEqualTo(config2.RequiredOnFailure);
    }
}

/// <summary>
/// Test compensation step class for unit testing.
/// </summary>
internal sealed class TestCompensationStep
{
}
