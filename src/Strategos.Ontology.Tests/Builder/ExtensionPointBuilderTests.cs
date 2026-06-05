using Strategos.Ontology.Builder;
using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Builder;

public interface ITestKnowledgeSource
{
    string Content { get; }
}

public class ExtensionPointBuilderTests
{
    [Test]
    public async Task Build_CreatesExtensionPointWithName()
    {
        var builder = new ExtensionPointBuilder("KnowledgeLinks");

        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.Name).IsEqualTo("KnowledgeLinks");
    }

    [Test]
    public async Task Description_SetsDescription()
    {
        var builder = new ExtensionPointBuilder("KnowledgeLinks");

        builder.Description("External knowledge sources");
        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.Description).IsEqualTo("External knowledge sources");
    }

    [Test]
    public async Task FromInterface_SetsRequiredSourceInterface()
    {
        var builder = new ExtensionPointBuilder("KnowledgeLinks");

        builder.FromInterface<ITestKnowledgeSource>();
        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.RequiredSourceInterface).IsEqualTo("ITestKnowledgeSource");
    }

    [Test]
    public async Task FromDomain_SetsRequiredSourceDomain()
    {
        var builder = new ExtensionPointBuilder("AdvisoryInputs");

        builder.FromDomain("style-engine");
        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.RequiredSourceDomain).IsEqualTo("style-engine");
    }

    // RequiresEdgeProperty_AddsRequiredEdgeProperty was removed in DR-5 (#120,
    // closes #114): IExtensionPointBuilder.RequiresEdgeProperty and
    // ExternalLinkExtensionPoint.RequiredEdgeProperties no longer exist. Edge
    // attributes now live on a reified Association<T>.

    [Test]
    public async Task MaxLinks_SetsMaxLinks()
    {
        var builder = new ExtensionPointBuilder("KnowledgeLinks");

        builder.MaxLinks(100);
        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.MaxLinks).IsEqualTo(100);
    }

    [Test]
    public async Task FluentChaining_AllMethods()
    {
        var builder = new ExtensionPointBuilder("KnowledgeLinks");

        builder
            .FromInterface<ITestKnowledgeSource>()
            .Description("External knowledge sources")
            .MaxLinks(100);

        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.Name).IsEqualTo("KnowledgeLinks");
        await Assert.That(extensionPoint.RequiredSourceInterface).IsEqualTo("ITestKnowledgeSource");
        await Assert.That(extensionPoint.MaxLinks).IsEqualTo(100);
    }

    [Test]
    public async Task DefaultValues_AreNullOrEmpty()
    {
        var builder = new ExtensionPointBuilder("Test");

        var extensionPoint = builder.Build();

        await Assert.That(extensionPoint.Description).IsNull();
        await Assert.That(extensionPoint.RequiredSourceInterface).IsNull();
        await Assert.That(extensionPoint.RequiredSourceDomain).IsNull();
        await Assert.That(extensionPoint.MaxLinks).IsNull();
        await Assert.That(extensionPoint.MatchedLinkNames.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ObjectTypeBuilder_AcceptsExternalLinks_Integration()
    {
        var builder = new ObjectTypeBuilder<TestStrategy>("Trading");
        builder.Key(s => s.Id);
        builder.Property(s => s.Name);

        builder.AcceptsExternalLinks("KnowledgeLinks", ext =>
        {
            ext.FromInterface<ITestKnowledgeSource>();
            ext.Description("External knowledge sources");
            ext.MaxLinks(100);
        });

        var descriptor = builder.Build();

        await Assert.That(descriptor.ExternalLinkExtensionPoints.Count).IsEqualTo(1);
        await Assert.That(descriptor.ExternalLinkExtensionPoints[0].Name).IsEqualTo("KnowledgeLinks");
    }

    [Test]
    public async Task ObjectTypeBuilder_MultipleExtensionPoints()
    {
        var builder = new ObjectTypeBuilder<TestStrategy>("Trading");
        builder.Key(s => s.Id);

        builder.AcceptsExternalLinks("KnowledgeLinks", ext =>
        {
            ext.FromInterface<ITestKnowledgeSource>();
        });

        builder.AcceptsExternalLinks("AdvisoryInputs", ext =>
        {
            ext.FromDomain("style-engine");
        });

        var descriptor = builder.Build();

        await Assert.That(descriptor.ExternalLinkExtensionPoints.Count).IsEqualTo(2);
    }
}
