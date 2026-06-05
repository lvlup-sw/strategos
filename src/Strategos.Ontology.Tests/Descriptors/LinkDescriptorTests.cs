using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class LinkDescriptorTests
{
    [Test]
    public async Task LinkCardinality_HasExpectedValues()
    {
        // Assert
        await Assert.That(Enum.IsDefined(LinkCardinality.OneToOne)).IsTrue();
        await Assert.That(Enum.IsDefined(LinkCardinality.OneToMany)).IsTrue();
        await Assert.That(Enum.IsDefined(LinkCardinality.ManyToMany)).IsTrue();
    }

    [Test]
    public async Task LinkDescriptor_Create_HasNameCardinalityAndTargetType()
    {
        // Arrange & Act
        var descriptor = new LinkDescriptor("Orders", "TradeOrder", LinkCardinality.OneToMany);

        // Assert
        await Assert.That(descriptor.Name).IsEqualTo("Orders");
        await Assert.That(descriptor.TargetTypeName).IsEqualTo("TradeOrder");
        await Assert.That(descriptor.Cardinality).IsEqualTo(LinkCardinality.OneToMany);
    }

    // LinkDescriptor_EdgeProperties_DefaultsEmpty was removed in DR-5 (#120,
    // closes #114): LinkDescriptor.EdgeProperties no longer exists. Edge
    // attributes now live on a reified Association<T>.

    [Test]
    public async Task LinkDescriptor_Description_DefaultsToNull()
    {
        // Arrange & Act
        var descriptor = new LinkDescriptor("Orders", "TradeOrder", LinkCardinality.OneToMany);

        // Assert
        await Assert.That(descriptor.Description).IsNull();
    }

    [Test]
    public async Task LinkDescriptor_WithDescription_StoresValue()
    {
        // Arrange & Act
        var descriptor = new LinkDescriptor("Orders", "TradeOrder", LinkCardinality.OneToMany)
        {
            Description = "Orders placed against this position",
        };

        // Assert
        await Assert.That(descriptor.Description).IsEqualTo("Orders placed against this position");
    }
}
