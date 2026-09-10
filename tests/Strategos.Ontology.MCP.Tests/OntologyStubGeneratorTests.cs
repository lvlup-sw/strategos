using Strategos.Ontology;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyStubGeneratorTests
{
    private OntologyGraph _graph = null!;
    private OntologyStubGenerator _generator = null!;

    [Before(Test)]
    public void Setup()
    {
        _graph = TestOntologyGraphFactory.CreateTradingGraph();
        _generator = new OntologyStubGenerator(_graph);
    }

    [Test]
    public async Task OntologyStubGenerator_Generate_ProducesValidPython()
    {
        // Act
        var stubs = _generator.Generate();

        // Assert — should produce stubs for each object type
        await Assert.That(stubs).HasCount().EqualTo(2); // TestPosition, TestOrder
        await Assert.That(stubs[0]).Contains("class ");
        await Assert.That(stubs[0]).Contains("\"\"\"");
    }

    [Test]
    public async Task OntologyStubGenerator_Generate_IncludesProperties()
    {
        // Act
        var stubs = _generator.Generate();
        var positionStub = stubs.First(s => s.Contains("TestPosition"));

        // Assert — properties should be listed
        await Assert.That(positionStub).Contains("Properties:");
        await Assert.That(positionStub).Contains("Symbol");
        await Assert.That(positionStub).Contains("required");
    }

    [Test]
    public async Task OntologyStubGenerator_Generate_IncludesActions()
    {
        // Act
        var stubs = _generator.Generate();
        var positionStub = stubs.First(s => s.Contains("TestPosition"));

        // Assert — actions should be listed
        await Assert.That(positionStub).Contains("Actions:");
        await Assert.That(positionStub).Contains("execute_trade");
        await Assert.That(positionStub).Contains("TestTradeExecutionRequest");
        await Assert.That(positionStub).Contains("TestTradeExecutionResult");
    }

    [Test]
    public async Task OntologyStubGenerator_Generate_IncludesLinks()
    {
        // Act
        var stubs = _generator.Generate();
        var positionStub = stubs.First(s => s.Contains("TestPosition"));

        // Assert — links should be listed
        await Assert.That(positionStub).Contains("Links:");
        await Assert.That(positionStub).Contains("Orders");
        await Assert.That(positionStub).Contains("TestOrder");
    }

    [Test]
    public async Task OntologyStubGenerator_Generate_IncludesEvents()
    {
        // Act
        var stubs = _generator.Generate();
        var positionStub = stubs.First(s => s.Contains("TestPosition"));

        // Assert — events should be listed
        await Assert.That(positionStub).Contains("Events:");
        await Assert.That(positionStub).Contains("TestTradeExecutedEvent");
    }

    [Test]
    public async Task OntologyStubGenerator_Generate_IncludesInterfaces()
    {
        // Act
        var stubs = _generator.Generate();
        var positionStub = stubs.First(s => s.Contains("TestPosition"));

        // Assert — interfaces should be listed
        await Assert.That(positionStub).Contains("Interfaces:");
        await Assert.That(positionStub).Contains("Searchable");
    }
}
