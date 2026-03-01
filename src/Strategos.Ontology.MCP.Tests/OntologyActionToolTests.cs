using Strategos.Ontology;
using Strategos.Ontology.Actions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyActionToolTests
{
    private OntologyGraph _graph = null!;
    private IActionDispatcher _actionDispatcher = null!;
    private IObjectSetProvider _objectSetProvider = null!;
    private OntologyActionTool _tool = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = TestOntologyGraphFactory.CreateTradingGraph();
        _actionDispatcher = Substitute.For<IActionDispatcher>();
        _objectSetProvider = Substitute.For<IObjectSetProvider>();
        _tool = new OntologyActionTool(_graph, _actionDispatcher, _objectSetProvider);
    }

    [Test]
    public async Task OntologyAction_SingleObject_DispatchesToActionDispatcher()
    {
        // Arrange
        var expectedResult = new ActionResult(true, Result: new { TradeId = "t1" });
        _actionDispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var request = new { Symbol = "AAPL", Quantity = 100 };

        // Act
        var result = await _tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "execute_trade",
            request: request,
            domain: "trading",
            objectId: "p1");

        // Assert
        await Assert.That(result.Results).HasCount().EqualTo(1);
        await Assert.That(result.Results[0].IsSuccess).IsTrue();

        // Verify dispatcher was called with the correct context
        await _actionDispatcher.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(ctx =>
                ctx.Domain == "trading"
                && ctx.ObjectType == "TestPosition"
                && ctx.ObjectId == "p1"
                && ctx.ActionName == "execute_trade"),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OntologyAction_WithFilter_AppliesBatch()
    {
        // Arrange
        var testItems = new List<object>
        {
            new TestPosition { Id = "p1", Symbol = "AAPL" },
            new TestPosition { Id = "p2", Symbol = "GOOGL" },
        };

        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        _actionDispatcher
            .DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(new ActionResult(true));

        var request = new { Quantity = 50 };

        // Act
        var result = await _tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "execute_trade",
            request: request,
            domain: "trading",
            filter: "Symbol == 'AAPL'");

        // Assert — should dispatch to each matched object
        await Assert.That(result.Results).HasCount().EqualTo(2);
        await _actionDispatcher.Received(2).DispatchAsync(
            Arg.Any<ActionContext>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OntologyAction_UnknownAction_ReturnsError()
    {
        // Act
        var result = await _tool.ExecuteAsync(
            objectType: "TestPosition",
            action: "nonexistent_action",
            request: new { },
            domain: "trading",
            objectId: "p1");

        // Assert
        await Assert.That(result.Results).HasCount().EqualTo(1);
        await Assert.That(result.Results[0].IsSuccess).IsFalse();
        await Assert.That(result.Results[0].Error).Contains("nonexistent_action");
    }

    [Test]
    public async Task OntologyAction_UnknownObjectType_ReturnsError()
    {
        // Act
        var result = await _tool.ExecuteAsync(
            objectType: "NonExistentType",
            action: "some_action",
            request: new { },
            domain: "trading",
            objectId: "p1");

        // Assert
        await Assert.That(result.Results).HasCount().EqualTo(1);
        await Assert.That(result.Results[0].IsSuccess).IsFalse();
        await Assert.That(result.Results[0].Error).Contains("NonExistentType");
    }
}
