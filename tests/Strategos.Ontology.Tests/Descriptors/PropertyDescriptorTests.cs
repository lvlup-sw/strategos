using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class PropertyDescriptorTests
{
    [Test]
    public async Task PropertyDescriptor_Create_HasNameAndType()
    {
        // Arrange & Act
        var descriptor = new PropertyDescriptor("Symbol", typeof(string));

        // Assert
        await Assert.That(descriptor.Name).IsEqualTo("Symbol");
        await Assert.That(descriptor.PropertyType).IsEqualTo(typeof(string));
    }

    [Test]
    public async Task PropertyDescriptor_Required_DefaultsFalse()
    {
        // Arrange & Act
        var descriptor = new PropertyDescriptor("Symbol", typeof(string));

        // Assert
        await Assert.That(descriptor.IsRequired).IsEqualTo(false);
    }

    [Test]
    public async Task PropertyDescriptor_Computed_DefaultsFalse()
    {
        // Arrange & Act
        var descriptor = new PropertyDescriptor("Symbol", typeof(string));

        // Assert
        await Assert.That(descriptor.IsComputed).IsEqualTo(false);
    }

    [Test]
    public async Task PropertyDescriptor_VectorDimensions_DefaultsNull()
    {
        // Arrange & Act
        var descriptor = new PropertyDescriptor("Embedding", typeof(float[]));

        // Assert
        await Assert.That(descriptor.VectorDimensions).IsNull();
    }

    [Test]
    public async Task PropertyDescriptor_VectorDimensions_CanBeSet()
    {
        // Arrange & Act
        var descriptor = new PropertyDescriptor("Embedding", typeof(float[]))
        {
            VectorDimensions = 1536,
            Kind = PropertyKind.Vector,
        };

        // Assert
        await Assert.That(descriptor.VectorDimensions).IsEqualTo(1536);
        await Assert.That(descriptor.Kind).IsEqualTo(PropertyKind.Vector);
    }
}
