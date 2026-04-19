using Microsoft.Extensions.Logging.Abstractions;

using Strategos.Ontology;
using Strategos.Ontology.Builder;
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
        _tool = new OntologyQueryTool(_graph, _objectSetProvider, _eventStreamProvider, NullLogger<OntologyQueryTool>.Instance);
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
        await Assert.That(result.Items).Count().IsEqualTo(1);
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
        await Assert.That(result.Items).Count().IsEqualTo(1);
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
        await Assert.That(result.Items).Count().IsEqualTo(1);
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
        await Assert.That(result.Items).Count().IsEqualTo(1);
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
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task QueryAsync_WithDomain_UsesResolvedClrType()
    {
        // Arrange
        ObjectSetExpression? capturedExpression = null;
        var testItems = new List<object> { new { Id = "p1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<ObjectSetExpression>();
                return new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties);
            });

        // Act
        await _tool.QueryAsync(objectType: "TestPosition", domain: "trading");

        // Assert — RootExpression should use TestPosition CLR type, not typeof(object)
        await Assert.That(capturedExpression).IsNotNull();
        var root = capturedExpression as RootExpression;
        await Assert.That(root).IsNotNull();
        await Assert.That(root!.ObjectType).IsEqualTo(typeof(TestPosition));
    }

    [Test]
    public async Task QueryAsync_UnknownObjectType_FallsBackToObjectType()
    {
        // Arrange
        ObjectSetExpression? capturedExpression = null;
        var testItems = new List<object>();
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<ObjectSetExpression>();
                return new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties);
            });

        // Act
        await _tool.QueryAsync(objectType: "NonExistentType", domain: "trading");

        // Assert — should fall back to typeof(object) when type is not found
        await Assert.That(capturedExpression).IsNotNull();
        var root = capturedExpression as RootExpression;
        await Assert.That(root).IsNotNull();
        await Assert.That(root!.ObjectType).IsEqualTo(typeof(object));
    }

    [Test]
    public async Task QueryAsync_TraverseLink_UsesResolvedClrType()
    {
        // Arrange
        ObjectSetExpression? capturedExpression = null;
        var testItems = new List<object> { new { OrderId = "o1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<ObjectSetExpression>();
                return new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties);
            });

        // Act
        await _tool.QueryAsync(objectType: "TestPosition", domain: "trading", traverseLink: "Orders");

        // Assert — TraverseLinkExpression should use resolved CLR type, not typeof(object)
        await Assert.That(capturedExpression).IsNotNull();
        var traverse = capturedExpression as TraverseLinkExpression;
        await Assert.That(traverse).IsNotNull();
        await Assert.That(traverse!.ObjectType).IsEqualTo(typeof(TestPosition));
    }

    [Test]
    public async Task QueryAsync_WithInterface_UsesResolvedClrType()
    {
        // Arrange
        ObjectSetExpression? capturedExpression = null;
        var testItems = new List<object> { new { Id = "p1" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<ObjectSetExpression>();
                return new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties);
            });

        // Act
        await _tool.QueryAsync(objectType: "TestPosition", domain: "trading", interfaceName: "Searchable");

        // Assert — InterfaceNarrowExpression should use resolved CLR type, not typeof(object)
        await Assert.That(capturedExpression).IsNotNull();
        var narrow = capturedExpression as InterfaceNarrowExpression;
        await Assert.That(narrow).IsNotNull();
        await Assert.That(narrow!.ObjectType).IsEqualTo(typeof(TestPosition));
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
        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].EventType).IsEqualTo("TradeExecuted");
    }

    [Test]
    public async Task QueryAsync_WithSemanticQuery_BuildsSimilarityExpression()
    {
        // Arrange
        SimilarityExpression? capturedExpression = null;
        var testItems = new List<object> { new { Id = "p1", Symbol = "AAPL" } };
        var testScores = new List<double> { 0.92 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<SimilarityExpression>();
                return new ScoredObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties, testScores);
            });

        // Act
        await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "high-value tech stocks");

        // Assert — ExecuteSimilarityAsync should have been called
        await Assert.That(capturedExpression).IsNotNull();
        await Assert.That(capturedExpression!.QueryText).IsEqualTo("high-value tech stocks");
    }

    [Test]
    public async Task QueryAsync_WithSemanticQuery_ReturnsSemanticQueryResult()
    {
        // Arrange
        var testItems = new List<object> { new { Id = "p1", Symbol = "AAPL" } };
        var testScores = new List<double> { 0.92 };
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ScoredObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties, testScores));

        // Act
        var result = await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "high-value tech stocks");

        // Assert
        await Assert.That(result).IsTypeOf<SemanticQueryResult>();
        var semanticResult = (SemanticQueryResult)result;
        await Assert.That(semanticResult.Scores).Count().IsEqualTo(1);
        await Assert.That(semanticResult.Scores[0]).IsEqualTo(0.92);
        await Assert.That(semanticResult.SemanticQuery).IsEqualTo("high-value tech stocks");
    }

    [Test]
    public async Task QueryAsync_WithSemanticQuery_SetsDefaultTopKAndMinRelevance()
    {
        // Arrange
        SimilarityExpression? capturedExpression = null;
        var testItems = new List<object>();
        var testScores = new List<double>();
        _objectSetProvider
            .ExecuteSimilarityAsync<object>(Arg.Any<SimilarityExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<SimilarityExpression>();
                return new ScoredObjectSetResult<object>(testItems, 0, ObjectSetInclusion.Properties, testScores);
            });

        // Act
        await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading",
            semanticQuery: "any query");

        // Assert — defaults should be TopK=5, MinRelevance=0.7
        await Assert.That(capturedExpression).IsNotNull();
        await Assert.That(capturedExpression!.TopK).IsEqualTo(5);
        await Assert.That(capturedExpression.MinRelevance).IsEqualTo(0.7);
    }

    [Test]
    public async Task QueryAsync_WithoutSemanticQuery_ReturnsRegularQueryResult()
    {
        // Arrange
        var testItems = new List<object> { new { Id = "p1", Symbol = "AAPL" } };
        _objectSetProvider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(new ObjectSetResult<object>(testItems, testItems.Count, ObjectSetInclusion.Properties));

        // Act
        var result = await _tool.QueryAsync(
            objectType: "TestPosition",
            domain: "trading");

        // Assert — without semanticQuery, should return plain QueryResult, not SemanticQueryResult
        await Assert.That(result).IsTypeOf<QueryResult>();
        await Assert.That(result.ObjectType).IsEqualTo("TestPosition");
        await Assert.That(result.Items).Count().IsEqualTo(1);
    }

    [Test]
    public async Task QueryAsync_WithExplicitDescriptorName_ThreadsNameIntoRootExpression()
    {
        // Arrange — register TestPosition under an explicit descriptor name that
        // differs from typeof(TestPosition).Name. The MCP tool receives the descriptor
        // name as its "objectType" parameter and must carry it through into the root
        // expression so providers dispatch against the correct partition.
        var graph = BuildExplicitNameGraph();
        var provider = Substitute.For<IObjectSetProvider>();
        var eventStream = Substitute.For<IEventStreamProvider>();
        var tool = new OntologyQueryTool(graph, provider, eventStream, NullLogger<OntologyQueryTool>.Instance);

        ObjectSetExpression? capturedExpression = null;
        provider
            .ExecuteAsync<object>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedExpression = callInfo.Arg<ObjectSetExpression>();
                return new ObjectSetResult<object>([], 0, ObjectSetInclusion.Properties);
            });

        // Act
        await tool.QueryAsync(objectType: "trading_documents", domain: "explicit-name-test");

        // Assert — the RootExpression must carry the explicit descriptor name
        // ("trading_documents"), NOT the CLR type name ("TestPosition").
        await Assert.That(capturedExpression).IsNotNull();
        await Assert.That(capturedExpression!.RootObjectTypeName).IsEqualTo("trading_documents");

        var root = capturedExpression as RootExpression;
        await Assert.That(root).IsNotNull();
        await Assert.That(root!.ObjectTypeName).IsEqualTo("trading_documents");
        // Ensure the CLR type was still correctly resolved from the graph.
        await Assert.That(root.ObjectType).IsEqualTo(typeof(TestPosition));
    }

    private static OntologyGraph BuildExplicitNameGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<ExplicitNameTestDomain>();
        return builder.Build();
    }
}

internal sealed class ExplicitNameTestDomain : DomainOntology
{
    public override string DomainName => "explicit-name-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>("trading_documents", obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });
    }
}
