using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public class TestOrder
{
    public string OrderId { get; set; } = "";
    public string PositionId { get; set; } = "";
}

public class TestAccount
{
    public string AccountId { get; set; } = "";
}

public class TestLinkedTradingOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.HasMany<TestOrder>("Orders");
        });

        builder.Object<TestOrder>(obj =>
        {
            obj.Key(o => o.OrderId);
            obj.HasOne<TestPosition>("Position");
        });

        builder.Object<TestAccount>(obj =>
        {
            obj.Key(a => a.AccountId);
            obj.HasMany<TestPosition>("Positions");
        });
    }
}

public class TestCircularOntology : DomainOntology
{
    public override string DomainName => "circular";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TestOrder>("Orders");
        });

        builder.Object<TestOrder>(obj =>
        {
            obj.Key(o => o.OrderId);
            obj.HasOne<TestPosition>("Position");
        });
    }
}

public class OntologyGraphTraversalTests
{
    [Test]
    public async Task OntologyGraph_TraverseLinks_Depth1_ReturnsDirectLinks()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestLinkedTradingOntology>();
        var graph = graphBuilder.Build();

        var results = graph.TraverseLinks("trading", "TestPosition", maxDepth: 1);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].ObjectType.Name).IsEqualTo("TestOrder");
        await Assert.That(results[0].LinkName).IsEqualTo("Orders");
        await Assert.That(results[0].Depth).IsEqualTo(1);
    }

    [Test]
    public async Task OntologyGraph_TraverseLinks_Depth2_ReturnsTransitiveLinks()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestLinkedTradingOntology>();
        var graph = graphBuilder.Build();

        var results = graph.TraverseLinks("trading", "TestAccount", maxDepth: 2);

        // Depth 1: TestAccount -> TestPosition (via "Positions")
        // Depth 2: TestPosition -> TestOrder (via "Orders")
        await Assert.That(results.Count).IsGreaterThanOrEqualTo(2);

        var depth1 = results.Where(r => r.Depth == 1).ToList();
        var depth2 = results.Where(r => r.Depth == 2).ToList();

        await Assert.That(depth1).Count().IsEqualTo(1);
        await Assert.That(depth1[0].ObjectType.Name).IsEqualTo("TestPosition");
        await Assert.That(depth2).Count().IsEqualTo(1);
        await Assert.That(depth2[0].ObjectType.Name).IsEqualTo("TestOrder");
    }

    [Test]
    public async Task OntologyGraph_TraverseLinks_MaxDepth_Respected()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestLinkedTradingOntology>();
        var graph = graphBuilder.Build();

        var results = graph.TraverseLinks("trading", "TestAccount", maxDepth: 1);

        // Should only include depth 1 (TestPosition), not depth 2 (TestOrder)
        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].ObjectType.Name).IsEqualTo("TestPosition");
    }

    [Test]
    public async Task OntologyGraph_TraverseLinks_CircularLinks_DoesNotLoop()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestCircularOntology>();
        var graph = graphBuilder.Build();

        // TestPosition -> TestOrder -> TestPosition (circular)
        // Should not loop infinitely
        var results = graph.TraverseLinks("circular", "TestPosition", maxDepth: 10);

        // Should visit TestOrder at depth 1, then TestPosition at depth 2 (already visited, so stop)
        await Assert.That(results.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(results.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task OntologyGraph_FindWorkflowChains_ExistingWorkflow_ReturnsChains()
    {
        // Build a graph with workflow chains manually via internal constructor
        var positionType = new ObjectTypeDescriptor("Position", typeof(TestPosition), "trading");
        var orderType = new ObjectTypeDescriptor("Order", typeof(TestOrder), "trading");

        var chain = new WorkflowChain("OrderExecution", orderType, positionType);

        var graph = new OntologyGraph(
            domains: Array.Empty<DomainDescriptor>(),
            objectTypes: new[] { positionType, orderType },
            interfaces: Array.Empty<InterfaceDescriptor>(),
            crossDomainLinks: Array.Empty<ResolvedCrossDomainLink>(),
            workflowChains: new[] { chain });

        var result = graph.FindWorkflowChains("OrderExecution");

        await Assert.That(result).Count().IsEqualTo(1);
        await Assert.That(result[0].WorkflowName).IsEqualTo("OrderExecution");
        await Assert.That(result[0].ConsumedType.Name).IsEqualTo("Order");
        await Assert.That(result[0].ProducedType.Name).IsEqualTo("Position");
    }

    [Test]
    public async Task TraverseLinks_ResultIncludesLinkDescription()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new TestDescribedLinkOntology());
        var graph = graphBuilder.Build();

        var results = graph.TraverseLinks("trading", "TestPosition", maxDepth: 1);

        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].LinkName).IsEqualTo("Orders");
        await Assert.That(results[0].Description).IsEqualTo("Orders placed against this position");
    }

    [Test]
    public async Task OntologyGraph_FindWorkflowChains_UnknownWorkflow_ReturnsEmpty()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingOntology>();
        var graph = graphBuilder.Build();

        var result = graph.FindWorkflowChains("NonExistent");

        await Assert.That(result).Count().IsEqualTo(0);
    }
}

public class TestDescribedLinkOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
            obj.HasMany<TestOrder>("Orders")
                .Description("Orders placed against this position");
        });

        builder.Object<TestOrder>(obj =>
        {
            obj.Key(o => o.OrderId);
        });
    }
}
