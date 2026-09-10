using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public class OntologyGraphLookupTests
{
    private static OntologyGraph BuildGraphWithTwoDomains()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingWithCrossDomainLinkOntology>();
        graphBuilder.AddDomain<TestMarketDataOntology>();
        return graphBuilder.Build();
    }

    [Test]
    public async Task OntologyGraph_GetObjectType_ExistingType_ReturnsDescriptor()
    {
        var graph = BuildGraphWithTwoDomains();

        var result = graph.GetObjectType("trading", "TestPosition");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("TestPosition");
        await Assert.That(result.DomainName).IsEqualTo("trading");
    }

    [Test]
    public async Task OntologyGraph_GetObjectType_UnknownType_ReturnsNull()
    {
        var graph = BuildGraphWithTwoDomains();

        var result = graph.GetObjectType("trading", "NonExistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task OntologyGraph_GetObjectType_WithDomain_FiltersCorrectly()
    {
        var graph = BuildGraphWithTwoDomains();

        var tradingResult = graph.GetObjectType("trading", "TestPosition");
        var marketDataResult = graph.GetObjectType("market-data", "TestInstrument");
        var crossResult = graph.GetObjectType("market-data", "TestPosition");

        await Assert.That(tradingResult).IsNotNull();
        await Assert.That(marketDataResult).IsNotNull();
        await Assert.That(crossResult).IsNull();
    }

    [Test]
    public async Task OntologyGraph_GetImplementors_ExistingInterface_ReturnsImplementors()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestDomainWithValidInterfaceOntology>();
        var graph = graphBuilder.Build();

        var implementors = graph.GetImplementors("IIdentifiable");

        await Assert.That(implementors).HasCount().EqualTo(1);
        await Assert.That(implementors[0].Name).IsEqualTo("IdentifiablePosition");
    }

    [Test]
    public async Task OntologyGraph_GetImplementors_UnknownInterface_ReturnsEmpty()
    {
        var graph = BuildGraphWithTwoDomains();

        var implementors = graph.GetImplementors("INonExistent");

        await Assert.That(implementors).HasCount().EqualTo(0);
    }
}
