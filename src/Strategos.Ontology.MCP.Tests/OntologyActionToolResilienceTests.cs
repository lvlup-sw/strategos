using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using Strategos.Ontology;
using Strategos.Ontology.Actions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

/// <summary>
/// Tests for resilience improvements in <see cref="OntologyActionTool"/>:
/// - Improved error messages with available domains/actions
/// - Logger-based exception handling in batch dispatch
/// - Null-forgiving operator removal
/// </summary>
public class OntologyActionToolResilienceTests
{
    private OntologyGraph _graph = null!;
    private IActionDispatcher _actionDispatcher = null!;
    private IObjectSetProvider _objectSetProvider = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = TestOntologyGraphFactory.CreateTradingGraph();
        _actionDispatcher = Substitute.For<IActionDispatcher>();
        _objectSetProvider = Substitute.For<IObjectSetProvider>();
    }

    [Test]
    public async Task UnknownObjectType_ErrorMessage_IncludesAvailableDomains()
    {
        // Arrange
        var logger = NullLoggerFactory.Instance.CreateLogger<OntologyActionTool>();
        var tool = new OntologyActionTool(_graph, _actionDispatcher, _objectSetProvider, logger);

        // Act
        var result = await tool.ExecuteAsync(
            objectType: "NonExistentType",
            action: "some_action",
            request: new { },
            domain: "trading",
            objectId: "p1");

        // Assert - error should include available domains info
        await Assert.That(result.Results[0].Error).Contains("trading");
    }

    [Test]
    public async Task UnknownAction_ErrorMessage_IncludesAvailableActions()
    {
        // Arrange
        var logger = NullLoggerFactory.Instance.CreateLogger<OntologyActionTool>();
        var tool = new OntologyActionTool(_graph, _actionDispatcher, _objectSetProvider, logger);

        // Act
        var result = await tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "nonexistent_action",
            request: new { },
            domain: "trading",
            objectId: "p1");

        // Assert - error should include available actions
        await Assert.That(result.Results[0].Error).Contains("execute_trade");
    }

    [Test]
    public async Task BatchDispatch_WhenExceptionThrown_LogsWarning()
    {
        // Arrange
        var logger = Substitute.For<ILogger<OntologyActionTool>>();
        var tool = new OntologyActionTool(_graph, _actionDispatcher, _objectSetProvider, logger);

        var testItems = new List<object>
        {
            new TestPosition { Id = "p1", Symbol = "AAPL" },
        };

        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        _actionDispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Test exception"));

        // Act
        var result = await tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "execute_trade",
            request: new { },
            domain: "trading",
            filter: "Symbol == 'AAPL'");

        // Assert - should still return error result (not throw)
        await Assert.That(result.Results).HasCount().EqualTo(1);
        await Assert.That(result.Results[0].IsSuccess).IsFalse();
        await Assert.That(result.Results[0].Error).Contains("Test exception");

        // Verify logger was called
        logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public async Task Constructor_AcceptsLogger()
    {
        // Arrange & Act - should not throw
        var logger = NullLoggerFactory.Instance.CreateLogger<OntologyActionTool>();
        var tool = new OntologyActionTool(_graph, _actionDispatcher, _objectSetProvider, logger);

        // Assert - tool is created successfully
        await Assert.That(tool).IsNotNull();
    }
}
