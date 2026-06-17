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

    // CrossDomainLinkBuilder_WithEdge_RecordsEdgeProperties was removed in DR-5
    // (#120, closes #114): ICrossDomainLinkBuilder.WithEdge and
    // CrossDomainLinkDescriptor.EdgeProperties it exercised no longer exist.
    // Edge attributes now live on a reified Association<T>.

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
