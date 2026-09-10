using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class DomainDescriptorTests
{
    [Test]
    public async Task DomainDescriptor_Create_HasDomainName()
    {
        var descriptor = new DomainDescriptor("Trading");

        await Assert.That(descriptor.DomainName).IsEqualTo("Trading");
    }

    [Test]
    public async Task DomainDescriptor_ObjectTypes_DefaultsEmpty()
    {
        var descriptor = new DomainDescriptor("Trading");

        await Assert.That(descriptor.ObjectTypes).IsNotNull();
        await Assert.That(descriptor.ObjectTypes.Count).IsEqualTo(0);
    }
}
