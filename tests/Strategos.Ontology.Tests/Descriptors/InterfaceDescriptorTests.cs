using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class InterfaceDescriptorTests
{
    [Test]
    public async Task InterfaceDescriptor_Create_HasNameAndInterfaceType()
    {
        var descriptor = new InterfaceDescriptor("Searchable", typeof(IDisposable));

        await Assert.That(descriptor.Name).IsEqualTo("Searchable");
        await Assert.That(descriptor.InterfaceType).IsEqualTo(typeof(IDisposable));
    }

    [Test]
    public async Task InterfaceDescriptor_Properties_DefaultsEmpty()
    {
        var descriptor = new InterfaceDescriptor("Searchable", typeof(IDisposable));

        await Assert.That(descriptor.Properties).IsNotNull();
        await Assert.That(descriptor.Properties.Count).IsEqualTo(0);
    }
}
