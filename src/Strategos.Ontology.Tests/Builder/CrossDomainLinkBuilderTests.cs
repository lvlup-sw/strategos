using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public class CrossDomainLinkBuilderTests
{
    [Test]
    public async Task CrossDomainLinkBuilder_Build_ProducesLinkWithNameAndSource()
    {
        var builder = new CrossDomainLinkBuilder("KnowledgeInformsStrategy");

        builder.From<TestAtomicNote>();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Name).IsEqualTo("KnowledgeInformsStrategy");
        await Assert.That(descriptor.SourceType).IsEqualTo(typeof(TestAtomicNote));
    }

    [Test]
    public async Task CrossDomainLinkBuilder_FromToExternal_SetsSourceAndTarget()
    {
        var builder = new CrossDomainLinkBuilder("KnowledgeInformsStrategy");

        builder.From<TestAtomicNote>().ToExternal("trading", "Strategy");
        var descriptor = builder.Build();

        await Assert.That(descriptor.SourceType).IsEqualTo(typeof(TestAtomicNote));
        await Assert.That(descriptor.TargetDomain).IsEqualTo("trading");
        await Assert.That(descriptor.TargetTypeName).IsEqualTo("Strategy");
    }

    [Test]
    public async Task CrossDomainLinkBuilder_ManyToMany_SetsCardinality()
    {
        var builder = new CrossDomainLinkBuilder("KnowledgeInformsStrategy");

        builder.From<TestAtomicNote>().ToExternal("trading", "Strategy").ManyToMany();
        var descriptor = builder.Build();

        await Assert.That(descriptor.Cardinality).IsEqualTo(LinkCardinality.ManyToMany);
    }

    [Test]
    public async Task CrossDomainLinkBuilder_WithEdge_RecordsEdgeProperties()
    {
        var builder = new CrossDomainLinkBuilder("KnowledgeInformsStrategy");

        builder.From<TestAtomicNote>()
            .ToExternal("trading", "Strategy")
            .ManyToMany()
            .WithEdge(edge =>
            {
                edge.Property<double>("Relevance");
                edge.Property<string>("Rationale");
            });
        var descriptor = builder.Build();

        await Assert.That(descriptor.EdgeProperties.Count).IsEqualTo(2);
        await Assert.That(descriptor.EdgeProperties[0].Name).IsEqualTo("Relevance");
        await Assert.That(descriptor.EdgeProperties[1].Name).IsEqualTo("Rationale");
    }

    [Test]
    public async Task CrossDomainLinkBuilder_WithDescription_SetsDescription()
    {
        var builder = new CrossDomainLinkBuilder("KnowledgeInformsStrategy");

        builder.From<TestAtomicNote>()
            .ToExternal("trading", "Strategy")
            .Description("Cross-domain knowledge-to-strategy link");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Description).IsEqualTo("Cross-domain knowledge-to-strategy link");
    }

    [Test]
    public async Task CrossDomainLinkDescriptor_Description_DefaultsToNull()
    {
        var builder = new CrossDomainLinkBuilder("KnowledgeInformsStrategy");

        builder.From<TestAtomicNote>().ToExternal("trading", "Strategy");
        var descriptor = builder.Build();

        await Assert.That(descriptor.Description).IsNull();
    }
}
