using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class ObjectTypeDescriptorTests
{
    [Test]
    public async Task ObjectTypeDescriptor_Create_HasNameAndClrType()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.Name).IsEqualTo("Position");
        await Assert.That(descriptor.ClrType).IsEqualTo(typeof(string));
        await Assert.That(descriptor.DomainName).IsEqualTo("Trading");
    }

    [Test]
    public async Task ObjectTypeDescriptor_Properties_DefaultsEmpty()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.Properties).IsNotNull();
        await Assert.That(descriptor.Properties.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ObjectTypeDescriptor_Links_DefaultsEmpty()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.Links).IsNotNull();
        await Assert.That(descriptor.Links.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ObjectTypeDescriptor_Actions_DefaultsEmpty()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.Actions).IsNotNull();
        await Assert.That(descriptor.Actions.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ObjectTypeDescriptor_Actions_SnapshotsAssignedCollection()
    {
        var original = new[]
        {
            new ActionDescriptor(
                new ActionSubject("Trading", "Position"),
                "open",
                "Open the position."),
        };
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading")
        {
            Actions = original,
        };

        original[0] = new ActionDescriptor(
            new ActionSubject("Trading", "Position"),
            "close",
            "Close the position.");

        await Assert.That(descriptor.Actions).HasCount().EqualTo(1);
        await Assert.That(descriptor.Actions[0].Name).IsEqualTo("open");
    }

    [Test]
    public async Task ObjectTypeDescriptor_Events_DefaultsEmpty()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.Events).IsNotNull();
        await Assert.That(descriptor.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ObjectTypeDescriptor_Interfaces_DefaultsEmpty()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.ImplementedInterfaces).IsNotNull();
        await Assert.That(descriptor.ImplementedInterfaces.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ObjectTypeDescriptor_KeyProperty_IsNull()
    {
        var descriptor = new ObjectTypeDescriptor("Position", typeof(string), "Trading");

        await Assert.That(descriptor.KeyProperty).IsNull();
    }
}
