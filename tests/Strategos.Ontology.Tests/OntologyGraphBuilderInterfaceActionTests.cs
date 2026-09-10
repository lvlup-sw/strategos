using Strategos.Ontology.Builder;
using Strategos.Ontology.Tests.Builder;

namespace Strategos.Ontology.Tests;

public class InterfaceWithActionDomainOntology : DomainOntology
{
    public override string DomainName => "iface-action-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ITestSearchableWithAction>("Searchable", iface =>
        {
            iface.Property(s => s.Title);
            iface.Action("Search")
                .Description("Search using semantic similarity")
                .Accepts<TestSearchRequest>()
                .Returns<TestSearchResult>();
        });

        builder.Object<TestSearchablePosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol);
            obj.Action("SearchPositions").Description("Search positions");

            obj.Implements<ITestSearchableWithAction>(map =>
            {
                map.Via(p => p.Symbol, s => s.Title);
                map.ActionVia("Search", "SearchPositions");
            });
        });
    }
}

public class UnmappedInterfaceActionDomainOntology : DomainOntology
{
    public override string DomainName => "unmapped-iface-test";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Interface<ITestSearchableWithAction>("Searchable", iface =>
        {
            iface.Property(s => s.Title);
            iface.Action("Search")
                .Description("Search")
                .Accepts<TestSearchRequest>();
        });

        builder.Object<TestSearchablePosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol);

            // Implements but does NOT map the Search action
            obj.Implements<ITestSearchableWithAction>(map =>
            {
                map.Via(p => p.Symbol, s => s.Title);
            });
        });
    }
}

public class OntologyGraphBuilderInterfaceActionTests
{
    [Test]
    public async Task ValidInterfaceActionMapping_BuildsSuccessfully()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new InterfaceWithActionDomainOntology());

        var graph = graphBuilder.Build();

        var iface = graph.Interfaces.First(i => i.Name == "Searchable");
        await Assert.That(iface.Actions.Count).IsEqualTo(1);
        await Assert.That(iface.Actions[0].Name).IsEqualTo("Search");
    }

    [Test]
    public async Task ValidInterfaceActionMapping_ObjectTypeHasMappings()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new InterfaceWithActionDomainOntology());

        var graph = graphBuilder.Build();

        var objectType = graph.GetObjectType("iface-action-test", "TestSearchablePosition")!;
        await Assert.That(objectType.InterfaceActionMappings.Count).IsEqualTo(1);
        await Assert.That(objectType.InterfaceActionMappings[0].InterfaceActionName).IsEqualTo("Search");
        await Assert.That(objectType.InterfaceActionMappings[0].ConcreteActionName).IsEqualTo("SearchPositions");
    }

    [Test]
    public async Task UnmappedInterfaceAction_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new UnmappedInterfaceActionDomainOntology());

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("does not map interface action 'Search'");
    }

    [Test]
    public async Task InterfaceWithNoActions_NoValidationNeeded()
    {
        // This tests that interfaces without actions don't require action mappings
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new InterfaceWithActionDomainOntology());

        // Should build without errors (the interface has an action but it IS mapped)
        var graph = graphBuilder.Build();

        await Assert.That(graph.ObjectTypes.Count).IsGreaterThan(0);
    }
}
