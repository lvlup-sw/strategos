// =============================================================================
// <copyright file="ContextualAgentSelectorTests.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Infrastructure.Selection;
using Strategos.Primitives;
using Strategos.Selection;

using NSubstitute;

namespace Strategos.Infrastructure.Tests.Selection;

/// <summary>
/// Unit tests for <see cref="ContextualAgentSelector"/> covering contextual
/// Thompson Sampling agent selection with feature extraction.
/// </summary>
[Property("Category", "Unit")]
public class ContextualAgentSelectorTests
{
    private readonly IBeliefStore _beliefStore;
    private readonly ITaskFeatureExtractor _featureExtractor;
    private readonly IBeliefPriorFactory _priorFactory;

    public ContextualAgentSelectorTests()
    {
        _beliefStore = Substitute.For<IBeliefStore>();
        _featureExtractor = Substitute.For<ITaskFeatureExtractor>();
        _priorFactory = Substitute.For<IBeliefPriorFactory>();
    }

    // =============================================================================
    // A. Feature Extraction Tests
    // =============================================================================

    /// <summary>
    /// Verifies that selector uses feature extractor.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_UsesFeatureExtractor()
    {
        // Arrange
        var context = CreateContext("Implement a sorting algorithm");
        var features = new TaskFeatures
        {
            Category = TaskCategory.CodeGeneration,
            Complexity = 0.5,
            MatchedKeywords = ["implement", "algorithm"],
        };

        _featureExtractor.ExtractFeatures(context).Returns(features);
        _priorFactory.CreatePrior(Arg.Any<string>(), features)
            .Returns(callInfo => AgentBelief.CreatePrior(callInfo.ArgAt<string>(0), features.Category.ToString()));
        _beliefStore.GetBeliefAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        var result = await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert
        _featureExtractor.Received(1).ExtractFeatures(context);
        await Assert.That(result.IsSuccess).IsTrue();
    }

    /// <summary>
    /// Verifies that selector returns features in selection result.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_ReturnsFeaturesInResult()
    {
        // Arrange
        var context = CreateContext("Analyze the sales data");
        var features = new TaskFeatures
        {
            Category = TaskCategory.DataAnalysis,
            Complexity = 0.7,
            MatchedKeywords = ["analyze", "data"],
        };

        _featureExtractor.ExtractFeatures(context).Returns(features);
        _priorFactory.CreatePrior(Arg.Any<string>(), features)
            .Returns(callInfo => AgentBelief.CreatePrior(callInfo.ArgAt<string>(0), features.Category.ToString()));
        _beliefStore.GetBeliefAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        var result = await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Features).IsNotNull();
        await Assert.That(result.Value.Features!.Category).IsEqualTo(TaskCategory.DataAnalysis);
        await Assert.That(result.Value.Features.Complexity).IsEqualTo(0.7);
    }

    // =============================================================================
    // B. Prior Factory Tests
    // =============================================================================

    /// <summary>
    /// Verifies that selector uses prior factory for new agents.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_UsesPriorFactoryForNewAgents()
    {
        // Arrange
        var context = CreateContext("Search the web");
        var features = TaskFeatures.CreateForCategory(TaskCategory.WebSearch);

        _featureExtractor.ExtractFeatures(context).Returns(features);
        _priorFactory.CreatePrior(Arg.Any<string>(), features)
            .Returns(callInfo => AgentBelief.CreatePrior(callInfo.ArgAt<string>(0), features.Category.ToString()));
        _beliefStore.GetBeliefAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert - Prior factory called for each candidate (2 agents)
        _priorFactory.Received(2).CreatePrior(Arg.Any<string>(), features);
    }

    /// <summary>
    /// Verifies that selector does not use prior factory for existing agents.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_DoesNotUsePriorFactoryForExistingAgents()
    {
        // Arrange
        var context = CreateContext("Read the file");
        var features = TaskFeatures.CreateForCategory(TaskCategory.FileOperation);
        var existingBelief = new AgentBelief
        {
            AgentId = "agent-1",
            TaskCategory = "FileOperation",
            Alpha = 5.0,
            Beta = 2.0,
            ObservationCount = 5, // Agent has observations, so prior factory should not be used
        };

        _featureExtractor.ExtractFeatures(context).Returns(features);
        _beliefStore.GetBeliefAsync("agent-1", "FileOperation", Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Success(existingBelief));
        _beliefStore.GetBeliefAsync("agent-2", "FileOperation", Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));
        _priorFactory.CreatePrior("agent-2", features)
            .Returns(AgentBelief.CreatePrior("agent-2", features.Category.ToString()));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert - Prior factory only called for agent-2 (not agent-1)
        _priorFactory.DidNotReceive().CreatePrior("agent-1", Arg.Any<TaskFeatures>());
        _priorFactory.Received(1).CreatePrior("agent-2", features);
    }

    // =============================================================================
    // C. Selection Logic Tests
    // =============================================================================

    /// <summary>
    /// Verifies that selector returns error when no candidates available.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_NoCandidates_ReturnsError()
    {
        // Arrange
        var context = new AgentSelectionContext
        {
            WorkflowId = Guid.NewGuid(),
            StepName = "TestStep",
            TaskDescription = "Do something",
            AvailableAgents = ["agent-1", "agent-2"],
            ExcludedAgents = ["agent-1", "agent-2"], // All excluded
        };

        var features = TaskFeatures.CreateDefault();
        _featureExtractor.ExtractFeatures(context).Returns(features);

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        var result = await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error!.Code).IsEqualTo("SELECTOR_NO_CANDIDATES");
    }

    /// <summary>
    /// Verifies that selector selects from available agents.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_SelectsFromAvailableAgents()
    {
        // Arrange
        var context = CreateContext("Plan a strategy");
        var features = TaskFeatures.CreateForCategory(TaskCategory.Reasoning);

        _featureExtractor.ExtractFeatures(context).Returns(features);
        _priorFactory.CreatePrior(Arg.Any<string>(), features)
            .Returns(callInfo => AgentBelief.CreatePrior(callInfo.ArgAt<string>(0), features.Category.ToString()));
        _beliefStore.GetBeliefAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        var result = await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(new[] { "agent-1", "agent-2" }).Contains(result.Value.SelectedAgentId);
    }

    /// <summary>
    /// Verifies that selector respects exclusions.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_RespectsExclusions()
    {
        // Arrange
        var context = new AgentSelectionContext
        {
            WorkflowId = Guid.NewGuid(),
            StepName = "TestStep",
            TaskDescription = "Write text",
            AvailableAgents = ["agent-1", "agent-2", "agent-3"],
            ExcludedAgents = ["agent-1", "agent-3"],
        };

        var features = TaskFeatures.CreateForCategory(TaskCategory.TextGeneration);
        _featureExtractor.ExtractFeatures(context).Returns(features);
        _priorFactory.CreatePrior("agent-2", features)
            .Returns(AgentBelief.CreatePrior("agent-2", features.Category.ToString()));
        _beliefStore.GetBeliefAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);

        // Act
        var result = await selector.SelectAgentAsync(context).ConfigureAwait(false);

        // Assert - Only agent-2 should be selectable
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.SelectedAgentId).IsEqualTo("agent-2");
    }

    // =============================================================================
    // D. Outcome Recording Tests
    // =============================================================================

    /// <summary>
    /// Verifies that RecordOutcomeAsync updates belief with partial credit.
    /// </summary>
    [Test]
    public async Task RecordOutcomeAsync_UpdatesBeliefWithPartialCredit()
    {
        // Arrange
        var existingBelief = new AgentBelief
        {
            AgentId = "agent-1",
            TaskCategory = "CodeGeneration",
            Alpha = 3.0,
            Beta = 2.0,
        };

        _beliefStore.GetBeliefAsync("agent-1", "CodeGeneration", Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Success(existingBelief));
        _beliefStore.SaveBeliefAsync(Arg.Any<AgentBelief>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);
        var outcome = AgentOutcome.Succeeded(confidence: 0.8);

        // Act
        var result = await selector.RecordOutcomeAsync("agent-1", "CodeGeneration", outcome).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _ = _beliefStore.Received(1).SaveBeliefAsync(
            Arg.Is<AgentBelief>(b =>
                b.AgentId == "agent-1" &&
                b.TaskCategory == "CodeGeneration" &&
                Math.Abs(b.Alpha - 3.8) < 0.001 &&
                Math.Abs(b.Beta - 2.2) < 0.001),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that RecordOutcomeAsync creates belief if not exists.
    /// </summary>
    [Test]
    public async Task RecordOutcomeAsync_CreatesBeliefIfNotExists()
    {
        // Arrange
        _beliefStore.GetBeliefAsync("agent-1", "General", Arg.Any<CancellationToken>())
            .Returns(Result<AgentBelief>.Failure(Error.Create(ErrorType.NotFound, "NOT_FOUND", "Not found")));
        _beliefStore.SaveBeliefAsync(Arg.Any<AgentBelief>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));

        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory, randomSeed: 42);
        var outcome = AgentOutcome.Succeeded();

        // Act
        var result = await selector.RecordOutcomeAsync("agent-1", "General", outcome).ConfigureAwait(false);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        _ = _beliefStore.Received(1).SaveBeliefAsync(
            Arg.Is<AgentBelief>(b =>
                b.AgentId == "agent-1" &&
                b.TaskCategory == "General" &&
                b.Alpha == 3.0 && // Default prior (2) + 1 success
                b.Beta == 2.0),   // Default prior (2)
            Arg.Any<CancellationToken>());
    }

    // =============================================================================
    // E. Validation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that constructor throws on null belief store.
    /// </summary>
    [Test]
    public async Task Constructor_NullBeliefStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ContextualAgentSelector(null!, _featureExtractor, _priorFactory))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that constructor throws on null feature extractor.
    /// </summary>
    [Test]
    public async Task Constructor_NullFeatureExtractor_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ContextualAgentSelector(_beliefStore, null!, _priorFactory))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that constructor throws on null prior factory.
    /// </summary>
    [Test]
    public async Task Constructor_NullPriorFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new ContextualAgentSelector(_beliefStore, _featureExtractor, null!))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies that SelectAgentAsync throws on null context.
    /// </summary>
    [Test]
    public async Task SelectAgentAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory);

        // Act & Assert
        await Assert.That(async () => await selector.SelectAgentAsync(null!).ConfigureAwait(false))
            .Throws<ArgumentNullException>();
    }

    // =============================================================================
    // F. Interface Implementation Tests
    // =============================================================================

    /// <summary>
    /// Verifies that ContextualAgentSelector implements IAgentSelector.
    /// </summary>
    [Test]
    public async Task ContextualAgentSelector_ImplementsInterface()
    {
        // Arrange
        var selector = new ContextualAgentSelector(_beliefStore, _featureExtractor, _priorFactory);

        // Assert
        await Assert.That(selector).IsAssignableTo<IAgentSelector>();
    }

    // =============================================================================
    // Helper Methods
    // =============================================================================

    private static AgentSelectionContext CreateContext(string description) => new()
    {
        WorkflowId = Guid.NewGuid(),
        StepName = "TestStep",
        TaskDescription = description,
        AvailableAgents = ["agent-1", "agent-2"],
    };
}
