using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Telemetry;

namespace Strategos.Ontology.Tests.Telemetry;

public class TelemetryTests
{
    [Test]
    public async Task OntologyTelemetryContext_Create_HasAllRequiredFields()
    {
        // Arrange
        var links = new List<string> { "Contacts" };
        var events = new List<string> { "Created" };

        // Act
        var context = new OntologyTelemetryContext(
            Domain: "CRM",
            ObjectType: "Deal",
            ActionName: "Close",
            TraversedLinks: links,
            ProducedEvents: events,
            QueryDepth: 2,
            InclusionLevel: ObjectSetInclusion.Full);

        // Assert
        await Assert.That(context.Domain).IsEqualTo("CRM");
        await Assert.That(context.ObjectType).IsEqualTo("Deal");
        await Assert.That(context.ActionName).IsEqualTo("Close");
        await Assert.That(context.TraversedLinks).HasCount().EqualTo(1);
        await Assert.That(context.ProducedEvents).HasCount().EqualTo(1);
        await Assert.That(context.QueryDepth).IsEqualTo(2);
        await Assert.That(context.InclusionLevel).IsEqualTo(ObjectSetInclusion.Full);
    }

    [Test]
    public async Task OntologyTelemetryContext_TraversedLinks_DefaultsEmpty()
    {
        // Arrange & Act
        var context = new OntologyTelemetryContext(
            Domain: "CRM",
            ObjectType: "Contact");

        // Assert
        await Assert.That(context.TraversedLinks).HasCount().EqualTo(0);
    }

    [Test]
    public async Task OntologyTelemetryContext_ProducedEvents_DefaultsEmpty()
    {
        // Arrange & Act
        var context = new OntologyTelemetryContext(
            Domain: "CRM",
            ObjectType: "Contact");

        // Assert
        await Assert.That(context.ProducedEvents).HasCount().EqualTo(0);
    }

    [Test]
    public async Task IOntologyMetrics_InterfaceExists()
    {
        // Arrange & Act
        var type = typeof(IOntologyMetrics);

        // Assert
        await Assert.That(type.IsInterface).IsTrue();
    }
}
