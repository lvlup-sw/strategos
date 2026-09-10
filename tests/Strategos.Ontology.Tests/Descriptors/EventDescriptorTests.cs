using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Tests.Descriptors;

public class EventDescriptorTests
{
    [Test]
    public async Task EventSeverity_HasExpectedValues()
    {
        await Assert.That(Enum.IsDefined(EventSeverity.Info)).IsTrue();
        await Assert.That(Enum.IsDefined(EventSeverity.Warning)).IsTrue();
        await Assert.That(Enum.IsDefined(EventSeverity.Alert)).IsTrue();
        await Assert.That(Enum.IsDefined(EventSeverity.Critical)).IsTrue();
    }

    [Test]
    public async Task EventDescriptor_Create_HasEventTypeAndDescription()
    {
        var descriptor = new EventDescriptor(typeof(string), "A trade was executed");

        await Assert.That(descriptor.EventType).IsEqualTo(typeof(string));
        await Assert.That(descriptor.Description).IsEqualTo("A trade was executed");
    }

    [Test]
    public async Task EventDescriptor_MaterializesLink_RecordsLinkName()
    {
        var descriptor = new EventDescriptor(typeof(string), "A trade was executed")
        {
            MaterializedLinks = ["Orders"],
        };

        await Assert.That(descriptor.MaterializedLinks.Count).IsEqualTo(1);
        await Assert.That(descriptor.MaterializedLinks[0]).IsEqualTo("Orders");
    }

    [Test]
    public async Task EventDescriptor_UpdatesProperty_RecordsPropertyName()
    {
        var descriptor = new EventDescriptor(typeof(string), "A trade was executed")
        {
            UpdatedProperties = ["UnrealizedPnL"],
        };

        await Assert.That(descriptor.UpdatedProperties.Count).IsEqualTo(1);
        await Assert.That(descriptor.UpdatedProperties[0]).IsEqualTo("UnrealizedPnL");
    }

    [Test]
    public async Task EventDescriptor_Severity_DefaultsInfo()
    {
        var descriptor = new EventDescriptor(typeof(string), "A trade was executed");

        await Assert.That(descriptor.Severity).IsEqualTo(EventSeverity.Info);
    }
}
