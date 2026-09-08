using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public class OntologyGraphTests
{
    private static OntologyGraph BuildGraphWithTwoDomains()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingWithCrossDomainLinkOntology>();
        graphBuilder.AddDomain<TestMarketDataOntology>();
        return graphBuilder.Build();
    }

    [Test]
    public async Task OntologyGraph_Create_HasDomains()
    {
        var graph = BuildGraphWithTwoDomains();

        await Assert.That(graph.Domains).HasCount().EqualTo(2);
        await Assert.That(graph.Domains[0].DomainName).IsEqualTo("trading");
        await Assert.That(graph.Domains[1].DomainName).IsEqualTo("market-data");
    }

    [Test]
    public async Task OntologyGraph_Create_HasObjectTypes()
    {
        var graph = BuildGraphWithTwoDomains();

        await Assert.That(graph.ObjectTypes).HasCount().EqualTo(2);
    }

    [Test]
    public async Task OntologyGraph_Create_HasInterfaces()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestDomainWithValidInterfaceOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.Interfaces).HasCount().EqualTo(1);
        await Assert.That(graph.Interfaces[0].Name).IsEqualTo("IIdentifiable");
    }

    [Test]
    public async Task OntologyGraph_Create_HasCrossDomainLinks()
    {
        var graph = BuildGraphWithTwoDomains();

        await Assert.That(graph.CrossDomainLinks).HasCount().EqualTo(1);
        await Assert.That(graph.CrossDomainLinks[0].Name).IsEqualTo("PositionToInstrument");
    }

    [Test]
    public async Task OntologyGraph_Create_HasWorkflowChains()
    {
        var graph = BuildGraphWithTwoDomains();

        await Assert.That(graph.WorkflowChains).IsNotNull();
    }

    [Test]
    public async Task OntologyGraph_IsFrozen_CollectionsAreImmutable()
    {
        var graph = BuildGraphWithTwoDomains();

        // Arrays implement IReadOnlyList<T> but remain writable after a downcast. A frozen graph
        // therefore must not expose its internal arrays through the public collection surfaces.
        await Assert.That(graph.Domains is DomainDescriptor[]).IsFalse();
        await Assert.That(graph.ObjectTypes is ObjectTypeDescriptor[]).IsFalse();
        await Assert.That(graph.Interfaces is InterfaceDescriptor[]).IsFalse();
        await Assert.That(graph.CrossDomainLinks is ResolvedCrossDomainLink[]).IsFalse();
        await Assert.That(graph.WorkflowChains is WorkflowChain[]).IsFalse();
        await Assert.That(graph.Warnings is string[]).IsFalse();
        await Assert.That(graph.Domains[0].ObjectTypes is ObjectTypeDescriptor[]).IsFalse();
    }
}
