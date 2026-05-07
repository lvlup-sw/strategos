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

        await Assert.That(graph.Domains).Count().IsEqualTo(2);
        await Assert.That(graph.Domains[0].DomainName).IsEqualTo("trading");
        await Assert.That(graph.Domains[1].DomainName).IsEqualTo("market-data");
    }

    [Test]
    public async Task OntologyGraph_Create_HasObjectTypes()
    {
        var graph = BuildGraphWithTwoDomains();

        await Assert.That(graph.ObjectTypes).Count().IsEqualTo(2);
    }

    [Test]
    public async Task OntologyGraph_Create_HasInterfaces()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestDomainWithValidInterfaceOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.Interfaces).Count().IsEqualTo(1);
        await Assert.That(graph.Interfaces[0].Name).IsEqualTo("IIdentifiable");
    }

    [Test]
    public async Task OntologyGraph_Create_HasCrossDomainLinks()
    {
        var graph = BuildGraphWithTwoDomains();

        await Assert.That(graph.CrossDomainLinks).Count().IsEqualTo(1);
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

        // All collections should be backed by arrays (IReadOnlyList<T>)
        // Verify they are truly read-only by checking the runtime type is an array
        await Assert.That(graph.Domains).IsTypeOf<DomainDescriptor[]>();
        await Assert.That(graph.ObjectTypes).IsTypeOf<ObjectTypeDescriptor[]>();
        await Assert.That(graph.Interfaces).IsTypeOf<InterfaceDescriptor[]>();
        await Assert.That(graph.CrossDomainLinks).IsTypeOf<ResolvedCrossDomainLink[]>();
        await Assert.That(graph.WorkflowChains).IsTypeOf<WorkflowChain[]>();
    }
}
