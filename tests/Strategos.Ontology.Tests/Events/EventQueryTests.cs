using Strategos.Ontology.Events;

namespace Strategos.Ontology.Tests.Events;

public class EventQueryTests
{
    [Test]
    public async Task OntologyEvent_Create_HasTypeTimestampAndPayload()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var payload = new { Value = 42 };

        // Act
        var evt = new OntologyEvent("CRM", "Contact", "c-1", "Created", timestamp, payload);

        // Assert
        await Assert.That(evt.Domain).IsEqualTo("CRM");
        await Assert.That(evt.ObjectType).IsEqualTo("Contact");
        await Assert.That(evt.ObjectId).IsEqualTo("c-1");
        await Assert.That(evt.EventType).IsEqualTo("Created");
        await Assert.That(evt.Timestamp).IsEqualTo(timestamp);
        await Assert.That(evt.Payload).IsEqualTo(payload);
    }

    [Test]
    public async Task EventQuery_Create_HasDomainAndObjectType()
    {
        // Arrange & Act
        var query = new EventQuery("CRM", "Contact");

        // Assert
        await Assert.That(query.Domain).IsEqualTo("CRM");
        await Assert.That(query.ObjectTypeName).IsEqualTo("Contact");
        await Assert.That(query.ObjectId).IsNull();
        await Assert.That(query.Since).IsNull();
        await Assert.That(query.EventTypes).IsNull();
    }

    [Test]
    public async Task EventQuery_WithSince_FiltersByTime()
    {
        // Arrange
        var since = DateTimeOffset.UtcNow.AddHours(-1);

        // Act
        var query = new EventQuery("CRM", "Contact", Since: since);

        // Assert
        await Assert.That(query.Since).IsEqualTo(since);
    }

    [Test]
    public async Task EventQuery_WithEventTypes_FiltersByType()
    {
        // Arrange
        var eventTypes = new List<string> { "Created", "Updated" };

        // Act
        var query = new EventQuery("CRM", "Contact", EventTypes: eventTypes);

        // Assert
        await Assert.That(query.EventTypes).IsNotNull();
        await Assert.That(query.EventTypes!).HasCount().EqualTo(2);
    }
}
