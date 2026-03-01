using Strategos.Ontology;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyQueryToolTests
{
    private OntologyGraph _graph = null!;
    private IObjectSetProvider _objectSetProvider = null!;
    private IEventStreamProvider _eventStreamProvider = null!;
    private OntologyQueryTool _tool = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = TestOntologyGraphFactory.CreateTradingGraph();
        _objectSetProvider = Substitute.For<IObjectSetProvider>();
        _eventStreamProvider = Substitute.For<IEventStreamProvider>();
        _tool = new OntologyQueryTool(_graph, _objectSetProvider, _eventStreamProvider);
    }

    [Test]
    public async Task OntologyQuery_ByObjectType_ReturnsInstances()
    {
        // Arrange
        var testItems = new List<object> { new { Id = "p1", Symbol = "AAPL" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        // Act
        var result = await _tool.QueryAsync(objectType: "TestPosition", domain: "trading");

        // Assert
        await Assert.That(result.ObjectType).IsEqualTo("TestPosition");
        await Assert.That(result.Items).HasCount().EqualTo(1);
    }

    [Test]
    public async Task OntologyQuery_WithFilter_FiltersResults()
    {
        // Arrange
        var testItems = new List<object> { new { Id = "p1", Symbol = "AAPL" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        // Act
        var result = await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            filter: "Symbol == 'AAPL'");

        // Assert
        await Assert.That(result.ObjectType).IsEqualTo("TestPosition");
        await Assert.That(result.Filter).IsEqualTo("Symbol == 'AAPL'");
        await Assert.That(result.Items).HasCount().EqualTo(1);
    }

    [Test]
    public async Task OntologyQuery_TraverseLink_ReturnsLinkedObjects()
    {
        // Arrange
        var testItems = new List<object> { new { OrderId = "o1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        // Act
        var result = await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            traverseLink: "Orders");

        // Assert
        await Assert.That(result.TraverseLink).IsEqualTo("Orders");
        await Assert.That(result.Items).HasCount().EqualTo(1);
    }

    [Test]
    public async Task OntologyQuery_WithInterface_NarrowsToImplementors()
    {
        // Arrange
        var testItems = new List<object> { new { Id = "p1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        // Act
        var result = await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            interfaceName: "Searchable");

        // Assert
        await Assert.That(result.InterfaceName).IsEqualTo("Searchable");
        await Assert.That(result.Items).HasCount().EqualTo(1);
    }

    [Test]
    public async Task OntologyQuery_Include_ControlsReturnedData()
    {
        // Arrange
        var testItems = new List<object> { new { Id = "p1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Full));

        // Act
        var result = await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            include: "Full");

        // Assert
        await Assert.That(result.Include).IsEqualTo("Full");
        await Assert.That(result.Items).HasCount().EqualTo(1);
    }

    [Test]
    public async Task OntologyQuery_Events_ReturnTemporalEvents()
    {
        // Arrange
        var events = new List<OntologyEvent>
        {
            new("trading", "TestPosition", "p1", "TradeExecuted", DateTimeOffset.UtcNow, null),
        };
        _eventStreamProvider
            .QueryEventsAsync(Arg.Any<EventQuery>(), Arg.Any<CancellationToken>())
            .Returns(events.ToAsyncEnumerable());

        // Act
        var result = await _tool.QueryEventsAsync(
            objectType: "TestPosition",
            domain: "trading");

        // Assert
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].EventType).IsEqualTo("TradeExecuted");
    }
}
