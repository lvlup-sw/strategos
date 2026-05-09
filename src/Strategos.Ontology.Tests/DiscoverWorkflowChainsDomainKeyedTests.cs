using Strategos.Ontology.Builder;
using Strategos.Ontology.Extensions;

namespace Strategos.Ontology.Tests;

internal sealed class AlphaOrder
{
    public string Id { get; set; } = "";
}

internal sealed class AlphaResult
{
    public string Id { get; set; } = "";
}

internal sealed class BetaOrder
{
    public string Id { get; set; } = "";
}

internal sealed class BetaResult
{
    public string Id { get; set; } = "";
}

internal sealed class AlphaDomainOntology : DomainOntology
{
    public override string DomainName => "domain-alpha";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AlphaOrder>("Order", obj => obj.Key(o => o.Id));
        builder.Object<AlphaResult>("Result", obj => obj.Key(r => r.Id));
    }
}

internal sealed class BetaDomainOntology : DomainOntology
{
    public override string DomainName => "domain-beta";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<BetaOrder>("Order", obj => obj.Key(o => o.Id));
        builder.Object<BetaResult>("Result", obj => obj.Key(r => r.Id));
    }
}

internal sealed class GammaDomainOntology : DomainOntology
{
    public override string DomainName => "domain-gamma";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<AlphaOrder>("Order", obj => obj.Key(o => o.Id));
        builder.Object<AlphaResult>("Result", obj => obj.Key(r => r.Id));
    }
}

public class DiscoverWorkflowChainsDomainKeyedTests
{
    [Test]
    public async Task DiscoverWorkflowChains_CrossDomainSimpleNameSharing_ResolvesToCorrectDomain()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<AlphaDomainOntology>();
        graphBuilder.AddDomain<BetaDomainOntology>();
        graphBuilder.AddWorkflowMetadata(new[]
        {
            new WorkflowMetadataBuilder("alpha-workflow")
                .InDomain("domain-alpha")
                .Consumes<AlphaOrder>()
                .Produces<AlphaResult>(),
            new WorkflowMetadataBuilder("beta-workflow")
                .InDomain("domain-beta")
                .Consumes<BetaOrder>()
                .Produces<BetaResult>(),
        });

        var graph = graphBuilder.Build();

        await Assert.That(graph.WorkflowChains).HasCount().EqualTo(2);

        var alphaChain = graph.WorkflowChains.FirstOrDefault(c => c.WorkflowName == "alpha-workflow");
        await Assert.That(alphaChain).IsNotNull();
        await Assert.That(alphaChain!.ConsumedType.DomainName).IsEqualTo("domain-alpha");
        await Assert.That(alphaChain.ProducedType.DomainName).IsEqualTo("domain-alpha");

        var betaChain = graph.WorkflowChains.FirstOrDefault(c => c.WorkflowName == "beta-workflow");
        await Assert.That(betaChain).IsNotNull();
        await Assert.That(betaChain!.ConsumedType.DomainName).IsEqualTo("domain-beta");
        await Assert.That(betaChain.ProducedType.DomainName).IsEqualTo("domain-beta");

        await Assert.That(graph.Warnings.Where(w => w.Contains("alpha-workflow") || w.Contains("beta-workflow")))
            .HasCount().EqualTo(0);
    }

    [Test]
    public async Task DiscoverWorkflowChains_SingleDomainCommonCase_StillWorks()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain<GammaDomainOntology>();
        graphBuilder.AddWorkflowMetadata(new[]
        {
            new WorkflowMetadataBuilder("gamma-workflow")
                .InDomain("domain-gamma")
                .Consumes<AlphaOrder>()
                .Produces<AlphaResult>(),
        });

        var graph = graphBuilder.Build();

        await Assert.That(graph.WorkflowChains).HasCount().EqualTo(1);
        await Assert.That(graph.WorkflowChains[0].WorkflowName).IsEqualTo("gamma-workflow");
        await Assert.That(graph.WorkflowChains[0].ConsumedType.DomainName).IsEqualTo("domain-gamma");
        await Assert.That(graph.WorkflowChains[0].ProducedType.DomainName).IsEqualTo("domain-gamma");
        await Assert.That(graph.Warnings.Where(w => w.Contains("gamma-workflow"))).HasCount().EqualTo(0);
    }
}
