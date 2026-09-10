// =============================================================================
// <copyright file="DefaultBeliefPriorFactoryTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Infrastructure.Selection;
using Strategos.Selection;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="DefaultBeliefPriorFactory"/> covering prior creation
/// for contextual agent selection.
/// </summary>
[Property("Category", "Unit")]
public class DefaultBeliefPriorFactoryTests
{
    // =============================================================================
    // A. Default Factory Tests
    // =============================================================================

    /// <summary>
    /// Verifies that default factory returns Beta(2, 2) prior.
    /// </summary>
    [Test]
    public async Task CreatePrior_DefaultFactory_ReturnsBeta2_2()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.Alpha).IsEqualTo(AgentBelief.DefaultPriorAlpha);
        await Assert.That(belief.Beta).IsEqualTo(AgentBelief.DefaultPriorBeta);
    }

    /// <summary>
    /// Verifies that default factory uses correct agent ID.
    /// </summary>
    [Test]
    public async Task CreatePrior_DefaultFactory_SetsAgentId()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-123", features);

        // Assert
        await Assert.That(belief.AgentId).IsEqualTo("agent-123");
    }

    /// <summary>
    /// Verifies that default factory uses task category from features.
    /// </summary>
    [Test]
    public async Task CreatePrior_DefaultFactory_UsesTaskCategoryFromFeatures()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateForCategory(TaskCategory.CodeGeneration);

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.TaskCategory).IsEqualTo(TaskCategory.CodeGeneration.ToString());
    }

    /// <summary>
    /// Verifies that default factory sets observation count to zero.
    /// </summary>
    [Test]
    public async Task CreatePrior_DefaultFactory_SetsObservationCountToZero()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.ObservationCount).IsEqualTo(0);
    }

    // =============================================================================
    // B. Custom Alpha/Beta Tests
    // =============================================================================

    /// <summary>
    /// Verifies that factory with custom alpha/beta uses configured values.
    /// </summary>
    [Test]
    public async Task CreatePrior_WithCustomAlphaBeta_UsesConfiguredValues()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory(alpha: 3.0, beta: 5.0);
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.Alpha).IsEqualTo(3.0);
        await Assert.That(belief.Beta).IsEqualTo(5.0);
    }

    /// <summary>
    /// Verifies that factory with alpha=1, beta=1 creates uniform prior.
    /// </summary>
    [Test]
    public async Task CreatePrior_WithUniformPrior_ReturnsAlpha1Beta1()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory(alpha: 1.0, beta: 1.0);
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.Alpha).IsEqualTo(1.0);
        await Assert.That(belief.Beta).IsEqualTo(1.0);
        await Assert.That(belief.Mean).IsEqualTo(0.5);
    }

    /// <summary>
    /// Verifies that factory with optimistic prior (alpha > beta).
    /// </summary>
    [Test]
    public async Task CreatePrior_WithOptimisticPrior_ReturnsHigherAlpha()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory(alpha: 5.0, beta: 2.0);
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.Alpha).IsEqualTo(5.0);
        await Assert.That(belief.Beta).IsEqualTo(2.0);
        await Assert.That(belief.Mean).IsGreaterThan(0.5);
    }

    /// <summary>
    /// Verifies that factory with pessimistic prior (beta > alpha).
    /// </summary>
    [Test]
    public async Task CreatePrior_WithPessimisticPrior_ReturnsHigherBeta()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory(alpha: 2.0, beta: 5.0);
        var features = TaskFeatures.CreateDefault();

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.Alpha).IsEqualTo(2.0);
        await Assert.That(belief.Beta).IsEqualTo(5.0);
        await Assert.That(belief.Mean).IsLessThan(0.5);
    }

    // =============================================================================
    // C. Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that factory throws on negative alpha.
    /// </summary>
    [Test]
    public async Task Constructor_NegativeAlpha_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => new DefaultBeliefPriorFactory(alpha: -1.0, beta: 2.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that factory throws on negative beta.
    /// </summary>
    [Test]
    public async Task Constructor_NegativeBeta_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => new DefaultBeliefPriorFactory(alpha: 2.0, beta: -1.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that factory throws on zero alpha.
    /// </summary>
    [Test]
    public async Task Constructor_ZeroAlpha_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => new DefaultBeliefPriorFactory(alpha: 0.0, beta: 2.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that factory throws on zero beta.
    /// </summary>
    [Test]
    public async Task Constructor_ZeroBeta_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.That(() => new DefaultBeliefPriorFactory(alpha: 2.0, beta: 0.0))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Verifies that CreatePrior throws on null agentId.
    /// </summary>
    [Test]
    public async Task CreatePrior_NullAgentId_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateDefault();

        // Act & Assert
        await Assert.That(() => factory.CreatePrior(null!, features))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that CreatePrior throws on null features.
    /// </summary>
    [Test]
    public async Task CreatePrior_NullFeatures_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();

        // Act & Assert
        await Assert.That(() => factory.CreatePrior("agent-1", null!))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // D. Category-Specific Tests
    // =============================================================================

    /// <summary>
    /// Verifies that different task categories produce appropriate priors.
    /// </summary>
    [Test]
    [Arguments(TaskCategory.General)]
    [Arguments(TaskCategory.CodeGeneration)]
    [Arguments(TaskCategory.DataAnalysis)]
    [Arguments(TaskCategory.WebSearch)]
    [Arguments(TaskCategory.FileOperation)]
    [Arguments(TaskCategory.Reasoning)]
    [Arguments(TaskCategory.TextGeneration)]
    public async Task CreatePrior_AllCategories_SetsCorrectTaskCategory(TaskCategory category)
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateForCategory(category);

        // Act
        var belief = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief.TaskCategory).IsEqualTo(category.ToString());
    }

    // =============================================================================
    // E. Interface Implementation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that factory implements IBeliefPriorFactory.
    /// </summary>
    [Test]
    public async Task DefaultBeliefPriorFactory_ImplementsInterface()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();

        // Assert
        await Assert.That(factory).IsAssignableTo<IBeliefPriorFactory>();
    }

    // =============================================================================
    // F. Determinism Tests
    // =============================================================================

    /// <summary>
    /// Verifies that same inputs produce same outputs (except timestamp).
    /// </summary>
    [Test]
    public async Task CreatePrior_SameInput_ReturnsSameAlphaBeta()
    {
        // Arrange
        var factory = new DefaultBeliefPriorFactory();
        var features = TaskFeatures.CreateForCategory(TaskCategory.CodeGeneration);

        // Act
        var belief1 = factory.CreatePrior("agent-1", features);
        var belief2 = factory.CreatePrior("agent-1", features);

        // Assert
        await Assert.That(belief1.Alpha).IsEqualTo(belief2.Alpha);
        await Assert.That(belief1.Beta).IsEqualTo(belief2.Beta);
        await Assert.That(belief1.AgentId).IsEqualTo(belief2.AgentId);
        await Assert.That(belief1.TaskCategory).IsEqualTo(belief2.TaskCategory);
    }
}
