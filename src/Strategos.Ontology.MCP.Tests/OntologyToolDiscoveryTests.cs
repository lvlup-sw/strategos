using Strategos.Ontology;
using Strategos.Ontology.Builder;

namespace Strategos.Ontology.MCP.Tests;

public class OntologyToolDiscoveryTests
{
    [Test]
    public async Task Discover_ReturnsOntologyTools()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();

        // Assert — should return exactly 3 tools
        await Assert.That(tools).HasCount().EqualTo(3);
    }

    [Test]
    public async Task Discover_IncludesQueryActionExplore()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var toolNames = tools.Select(t => t.Name).ToList();

        // Assert
        await Assert.That(toolNames).Contains("ontology_query");
        await Assert.That(toolNames).Contains("ontology_action");
        await Assert.That(toolNames).Contains("ontology_explore");
    }

    [Test]
    public async Task Discover_EnrichWithOntology_AddsSemanticMetadata()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var exploreTool = tools.First(t => t.Name == "ontology_explore");

        // Assert — enriched description should reference the ontology domains
        await Assert.That(exploreTool.Description).Contains("trading");
        await Assert.That(exploreTool.Description.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Discover_ActionToolIncludesConstraintSummaries()
    {
        // Arrange
        var graph = CreateConstrainedGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var actionTool = tools.First(t => t.Name == "ontology_action");

        // Assert — should have constraint summaries for the constrained action
        await Assert.That(actionTool.ConstraintSummaries).HasCount().GreaterThanOrEqualTo(1);
        await Assert.That(actionTool.ConstraintSummaries.Select(s => s.ActionName))
            .Contains("close_account");
    }

    [Test]
    public async Task Discover_ConstraintSummary_CountsHardAndSoft()
    {
        // Arrange
        var graph = CreateConstrainedGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var actionTool = tools.First(t => t.Name == "ontology_action");
        var summary = actionTool.ConstraintSummaries
            .First(s => s.ActionName == "close_account");

        // Assert — close_account has 2 hard (Requires + RequiresLink) and 1 soft (RequiresSoft)
        await Assert.That(summary.HardConstraintCount).IsEqualTo(2);
        await Assert.That(summary.SoftConstraintCount).IsEqualTo(1);
    }

    [Test]
    public async Task Discover_ConstraintSummary_IncludesDescriptions()
    {
        // Arrange
        var graph = CreateConstrainedGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var actionTool = tools.First(t => t.Name == "ontology_action");
        var summary = actionTool.ConstraintSummaries
            .First(s => s.ActionName == "close_account");

        // Assert — descriptions should be populated and non-empty
        await Assert.That(summary.ConstraintDescriptions).HasCount().EqualTo(3);
        await Assert.That(summary.ConstraintDescriptions.All(d => !string.IsNullOrWhiteSpace(d)))
            .IsTrue();
    }

    [Test]
    public async Task Discover_NoConstraints_EmptySummaries()
    {
        // Arrange — trading graph has no preconditions on its actions
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var actionTool = tools.First(t => t.Name == "ontology_action");

        // Assert
        await Assert.That(actionTool.ConstraintSummaries).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Discover_ExploreTool_HasEmptyConstraintSummaries()
    {
        // Arrange — even when the graph has constrained actions, explore tool should not carry them
        var graph = CreateConstrainedGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var exploreTool = tools.First(t => t.Name == "ontology_explore");

        // Assert — constraint summaries are only attached to ontology_action, not explore
        await Assert.That(exploreTool.ConstraintSummaries).HasCount().EqualTo(0);
    }

    [Test]
    public async Task Discover_QueryToolDescription_MentionsSemanticSearch()
    {
        // Arrange
        var graph = TestOntologyGraphFactory.CreateTradingGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var queryTool = tools.First(t => t.Name == "ontology_query");

        // Assert — description should mention semantic search capabilities
        await Assert.That(queryTool.Description).Contains("semantic search");
        await Assert.That(queryTool.Description).Contains("semanticQuery");
        await Assert.That(queryTool.Description).Contains("topK");
        await Assert.That(queryTool.Description).Contains("minRelevance");
        await Assert.That(queryTool.Description).Contains("distanceMetric");
    }

    [Test]
    public async Task Discover_ActionDescription_IncludesConstraintCount()
    {
        // Arrange
        var graph = CreateConstrainedGraph();
        var discovery = new OntologyToolDiscovery(graph);

        // Act
        var tools = discovery.Discover();
        var actionTool = tools.First(t => t.Name == "ontology_action");

        // Assert — description should mention the count of constrained actions
        await Assert.That(actionTool.Description).Contains("1 action(s)");
        await Assert.That(actionTool.Description).Contains("constraint rule(s)");
    }

    /// <summary>
    /// Creates a test ontology graph with constrained actions for testing
    /// constraint summary generation.
    /// </summary>
    private static OntologyGraph CreateConstrainedGraph()
    {
        var builder = new OntologyGraphBuilder();
        builder.AddDomain<ConstrainedDomainOntology>();
        return builder.Build();
    }
}

// Test domain types for constrained action tests

public class TestAccount
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "active";
    public decimal Balance { get; set; }
}

public class TestTransaction
{
    public string TransactionId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class TestCloseAccountRequest
{
    public string Reason { get; set; } = "";
}

public class TestCloseAccountResult
{
    public bool Success { get; set; }
}

/// <summary>
/// A test domain ontology with actions that have preconditions, used to verify
/// constraint summary generation in OntologyToolDiscovery.
/// </summary>
public class ConstrainedDomainOntology : DomainOntology
{
    public override string DomainName => "banking";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestAccount>(obj =>
        {
            obj.Key(a => a.Id);
            obj.Property(a => a.Status).Required();
            obj.Property(a => a.Balance);
            obj.HasMany<TestTransaction>("Transactions");

            // Action with 2 hard constraints and 1 soft constraint
            obj.Action("close_account")
                .Description("Close the account permanently")
                .Accepts<TestCloseAccountRequest>()
                .Returns<TestCloseAccountResult>()
                .Requires(a => a.Status == "active")
                .RequiresLink("Transactions")
                .RequiresSoft(a => a.Balance == 0);
        });

        builder.Object<TestTransaction>(obj =>
        {
            obj.Key(t => t.TransactionId);
            obj.Property(t => t.Amount).Required();
            obj.HasOne<TestAccount>("Account");
        });
    }
}
