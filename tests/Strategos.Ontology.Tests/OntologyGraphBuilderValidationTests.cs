using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests;

public class TestTradingWithCrossDomainLinkOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
            obj.Property(p => p.Symbol).Required();
        });

        builder.CrossDomainLink("PositionToInstrument")
            .From<TestPosition>()
            .ToExternal("market-data", "TestInstrument")
            .ManyToMany();
    }
}

public class TestTradingWithBadDomainLinkOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
        });

        builder.CrossDomainLink("PositionToUnknown")
            .From<TestPosition>()
            .ToExternal("nonexistent-domain", "SomeType");
    }
}

public class TestTradingWithBadTypeLinkOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TestPosition>(obj =>
        {
            obj.Key(p => p.Id);
        });

        builder.CrossDomainLink("PositionToUnknown")
            .From<TestPosition>()
            .ToExternal("market-data", "NonExistentType");
    }
}

public class OntologyGraphBuilderValidationTests
{
    [Test]
    public async Task OntologyGraphBuilder_Build_ValidCrossDomainLink_Succeeds()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingWithCrossDomainLinkOntology>();
        graphBuilder.AddDomain<TestMarketDataOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph.CrossDomainLinks).HasCount().EqualTo(1);
        await Assert.That(graph.CrossDomainLinks[0].Name).IsEqualTo("PositionToInstrument");
        await Assert.That(graph.CrossDomainLinks[0].SourceDomain).IsEqualTo("trading");
        await Assert.That(graph.CrossDomainLinks[0].TargetDomain).IsEqualTo("market-data");
        await Assert.That(graph.CrossDomainLinks[0].TargetObjectType.Name).IsEqualTo("TestInstrument");
        await Assert.That(graph.CrossDomainLinks[0].Cardinality).IsEqualTo(LinkCardinality.ManyToMany);
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_InvalidCrossDomainLink_ThrowsOntologyCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingWithBadDomainLinkOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_UnresolvableDomain_ThrowsWithDomainName()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingWithBadDomainLinkOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("nonexistent-domain");
    }

    [Test]
    public async Task OntologyGraphBuilder_Build_UnresolvableObjectType_ThrowsWithTypeName()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TestTradingWithBadTypeLinkOntology>();
        graphBuilder.AddDomain<TestMarketDataOntology>();

        await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithMessageContaining("NonExistentType");
    }
}
