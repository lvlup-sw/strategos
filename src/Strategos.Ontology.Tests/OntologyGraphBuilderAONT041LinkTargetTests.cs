using Strategos.Ontology.Builder;

namespace Strategos.Ontology.Tests;

public class TradeOrder
{
    public string Id { get; set; } = "";
    public string Symbol { get; set; } = "";
}

public class Portfolio
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TradeOrderExplicitNameOntology : DomainOntology
{
    public override string DomainName => "trading-explicit";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TradeOrder>("open_orders", obj =>
        {
            obj.Key(t => t.Id);
        });

        builder.Object<Portfolio>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TradeOrder>("Orders");
        });
    }
}

public class TradeOrderDefaultNameOntology : DomainOntology
{
    public override string DomainName => "trading-default";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<TradeOrder>(obj =>
        {
            obj.Key(t => t.Id);
        });

        builder.Object<Portfolio>(obj =>
        {
            obj.Key(p => p.Id);
            obj.HasMany<TradeOrder>("Orders");
        });
    }
}

public class OntologyGraphBuilderAONT041LinkTargetTests
{
    [Test]
    public async Task AONT041_LinkTargetWithExplicitDescriptorName_ThrowsCompositionException()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TradeOrderExplicitNameOntology>();

        var exception = await Assert.That(() => graphBuilder.Build())
            .ThrowsException()
            .WithExceptionType(typeof(OntologyCompositionException));

        await Assert.That(exception!.Message).Contains("AONT041");
        await Assert.That(exception.Message).Contains("open_orders");
    }

    [Test]
    public async Task AONT041_LinkTargetWithDefaultDescriptorName_DoesNotThrow()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<TradeOrderDefaultNameOntology>();

        var graph = graphBuilder.Build();

        await Assert.That(graph).IsNotNull();
        await Assert.That(graph.ObjectTypes.Any(ot => ot.Name == "TradeOrder")).IsTrue();
    }
}
