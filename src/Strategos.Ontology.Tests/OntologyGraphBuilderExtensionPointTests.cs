using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Tests.Builder;

namespace Strategos.Ontology.Tests;

public record ExtTestNote(Guid Id, string Title, string Content);
public record ExtTestStrategy(Guid Id, string Name);

public class ExtTestKnowledgeDomainOntology : DomainOntology
{
    public override string DomainName => "knowledge";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ExtTestNote>(obj =>
        {
            obj.Key(n => n.Id);
            obj.Property(n => n.Title).Required();
            obj.Property(n => n.Content);

            obj.Implements<ITestKnowledgeSource>(map =>
            {
                map.Via(n => n.Content, k => k.Content);
            });
        });

        builder.CrossDomainLink("KnowledgeInformsStrategy")
            .From<ExtTestNote>()
            .ToExternal("trading", "ExtTestStrategy")
            .ManyToMany();
    }
}

public class ExtTestTradingDomainOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ExtTestStrategy>(obj =>
        {
            obj.Key(s => s.Id);
            obj.Property(s => s.Name).Required();

            obj.AcceptsExternalLinks("KnowledgeLinks", ext =>
            {
                ext.FromInterface<ITestKnowledgeSource>();
                ext.Description("External knowledge sources that inform this strategy");
                ext.MaxLinks(100);
            });
        });
    }
}

public class ExtTestTradingNoDomainConstraintOntology : DomainOntology
{
    public override string DomainName => "trading";

    protected override void Define(IOntologyBuilder builder)
    {
        builder.Object<ExtTestStrategy>(obj =>
        {
            obj.Key(s => s.Id);
            obj.Property(s => s.Name).Required();

            obj.AcceptsExternalLinks("KnowledgeLinks", ext =>
            {
                ext.Description("Any external links");
            });
        });
    }
}

public class OntologyGraphBuilderExtensionPointTests
{
    [Test]
    public async Task ExtensionPointMatching_PopulatesMatchedLinkNames()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new ExtTestKnowledgeDomainOntology());
        graphBuilder.AddDomain(new ExtTestTradingNoDomainConstraintOntology());

        var graph = graphBuilder.Build();

        var strategy = graph.GetObjectType("trading", "ExtTestStrategy")!;
        await Assert.That(strategy.ExternalLinkExtensionPoints.Count).IsEqualTo(1);
        await Assert.That(strategy.ExternalLinkExtensionPoints[0].MatchedLinkNames.Count).IsEqualTo(1);
        await Assert.That(strategy.ExternalLinkExtensionPoints[0].MatchedLinkNames[0])
            .IsEqualTo("KnowledgeInformsStrategy");
    }

    [Test]
    public async Task ExtensionPointMatching_InterfaceConstraintUnsatisfied_AddsWarning()
    {
        // The knowledge domain's ExtTestNote implements ITestKnowledgeSource
        // The trading domain's extension point requires ITestKnowledgeSource
        // Since the cross-domain link resolves from ExtTestNote (which does implement),
        // this should match and NOT produce a warning
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new ExtTestKnowledgeDomainOntology());
        graphBuilder.AddDomain(new ExtTestTradingDomainOntology());

        var graph = graphBuilder.Build();

        var strategy = graph.GetObjectType("trading", "ExtTestStrategy")!;
        await Assert.That(strategy.ExternalLinkExtensionPoints[0].MatchedLinkNames.Count).IsEqualTo(1);
    }

    // ExtensionPointMatching_MissingEdgeProperty_ProducesWarning was removed in
    // DR-5 (#120, closes #114): the RequiresEdgeProperty / EdgeProperties
    // extension-point edge-matching surface that produced the warning was
    // removed. Edge attributes now live on a reified Association<T>.

    [Test]
    public async Task NoExtensionPoints_BuildsSuccessfully()
    {
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new DerivationDomainOntology());

        var graph = graphBuilder.Build();

        await Assert.That(graph.Warnings.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ExtensionPointWithNoMatchingLinks_MatchedLinkNamesEmpty()
    {
        // Trading domain has extension point but no knowledge domain is registered
        var graphBuilder = new OntologyGraphBuilder();
        graphBuilder.AddDomain(new ExtTestTradingDomainOntology());

        var graph = graphBuilder.Build();

        var strategy = graph.GetObjectType("trading", "ExtTestStrategy")!;
        await Assert.That(strategy.ExternalLinkExtensionPoints[0].MatchedLinkNames.Count).IsEqualTo(0);
    }
}
