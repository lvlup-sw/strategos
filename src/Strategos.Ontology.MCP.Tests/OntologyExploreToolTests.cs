using Strategos.Ontology;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyExploreToolTests
{
    private OntologyGraph _graph = null!;
    private OntologyExploreTool _tool = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = TestOntologyGraphFactory.CreateTradingGraph();
        _tool = new OntologyExploreTool(_graph);
    }

    [Test]
    public async Task OntologyExplore_Domains_ReturnsAllDomains()
    {
        // Act
        var result = _tool.Explore(scope: "domains");

        // Assert
        await Assert.That(result.Scope).IsEqualTo("domains");
        await Assert.That(result.Items).HasCount().EqualTo(1);

        var domain = result.Items[0];
        await Assert.That(domain.ContainsKey("domainName")).IsTrue();
        await Assert.That(domain["domainName"]?.ToString()).IsEqualTo("trading");
    }

    [Test]
    public async Task OntologyExplore_ObjectTypes_ReturnsTypesInDomain()
    {
        // Act
        var result = _tool.Explore(scope: "objectTypes", domain: "trading");

        // Assert
        await Assert.That(result.Scope).IsEqualTo("objectTypes");
        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(2);

        var typeNames = result.Items.Select(i => i["name"]?.ToString()).ToList();
        await Assert.That(typeNames).Contains("TestPosition");
        await Assert.That(typeNames).Contains("TestOrder");
    }

    [Test]
    public async Task OntologyExplore_Actions_ReturnsActionsOnObjectType()
    {
        // Act
        var result = _tool.Explore(scope: "actions", domain: "trading", objectType: "TestPosition");

        // Assert
        await Assert.That(result.Scope).IsEqualTo("actions");
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0]["name"]?.ToString()).IsEqualTo("execute_trade");
        await Assert.That(result.Items[0]["description"]?.ToString()).IsEqualTo("Execute a trade on the position");
    }

    [Test]
    public async Task OntologyExplore_Links_ReturnsLinksFromObjectType()
    {
        // Act
        var result = _tool.Explore(scope: "links", domain: "trading", objectType: "TestPosition");

        // Assert
        await Assert.That(result.Scope).IsEqualTo("links");
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0]["name"]?.ToString()).IsEqualTo("Orders");
        await Assert.That(result.Items[0]["targetTypeName"]?.ToString()).IsEqualTo("TestOrder");
    }

    [Test]
    public async Task OntologyExplore_Events_ReturnsEventsOnObjectType()
    {
        // Act
        var result = _tool.Explore(scope: "events", domain: "trading", objectType: "TestPosition");

        // Assert
        await Assert.That(result.Scope).IsEqualTo("events");
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0]["description"]?.ToString()).IsEqualTo("Trade was executed");
    }

    [Test]
    public async Task OntologyExplore_TraverseFrom_ReturnsGraphTraversal()
    {
        // Act
        var result = _tool.Explore(
            scope: "links",
            domain: "trading",
            objectType: "TestPosition",
            traverseFrom: "TestPosition",
            maxDepth: 2);

        // Assert — traversal from TestPosition should find TestOrder
        await Assert.That(result.Items.Count).IsGreaterThanOrEqualTo(1);

        var traversedTypeNames = result.Items.Select(i => i["objectType"]?.ToString()).ToList();
        await Assert.That(traversedTypeNames).Contains("TestOrder");
    }

    [Test]
    public async Task Explore_VectorProperties_ReturnsVectorAnnotatedTypes()
    {
        // Arrange — use vector graph which has TestDocument with an Embedding vector property
        var vectorGraph = TestOntologyGraphFactory.CreateVectorGraph();
        var vectorTool = new OntologyExploreTool(vectorGraph);

        // Act
        var result = vectorTool.Explore(scope: "vectorProperties");

        // Assert — should return only types with vector properties
        await Assert.That(result.Scope).IsEqualTo("vectorProperties");
        await Assert.That(result.Items).HasCount().EqualTo(1);
        await Assert.That(result.Items[0]["name"]?.ToString()).IsEqualTo("TestDocument");
        await Assert.That(result.Items[0]["domain"]?.ToString()).IsEqualTo("content");

        var vectorProps = result.Items[0]["vectorProperties"] as IReadOnlyList<Dictionary<string, object?>>;
        await Assert.That(vectorProps).IsNotNull();
        await Assert.That(vectorProps!).HasCount().EqualTo(1);
        await Assert.That(vectorProps[0]["name"]?.ToString()).IsEqualTo("Embedding");
        await Assert.That(vectorProps[0]["dimensions"]).IsEqualTo(1536);
    }

    [Test]
    public async Task Explore_ObjectTypes_IncludesIsSemanticSearchable()
    {
        // Arrange — use vector graph which has TestDocument (vector) and TestImage (no vector)
        var vectorGraph = TestOntologyGraphFactory.CreateVectorGraph();
        var vectorTool = new OntologyExploreTool(vectorGraph);

        // Act
        var result = vectorTool.Explore(scope: "objectTypes");

        // Assert
        var docType = result.Items.First(i => i["name"]?.ToString() == "TestDocument");
        var imgType = result.Items.First(i => i["name"]?.ToString() == "TestImage");

        await Assert.That(docType["isSemanticSearchable"]).IsEqualTo(true);
        await Assert.That(imgType["isSemanticSearchable"]).IsEqualTo(false);
    }
}
