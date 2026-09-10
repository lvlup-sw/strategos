using Strategos.Ontology.Events;

namespace Strategos.Ontology.Tests.Events;

public class EventProviderTests
{
    [Test]
    public async Task IEventStreamProvider_QueryEventsAsync_MethodSignatureExists()
    {
        // Arrange
        var provider = Substitute.For<IEventStreamProvider>();
        var query = new EventQuery("CRM", "Contact");
        var expected = new OntologyEvent("CRM", "Contact", "c-1", "Created", DateTimeOffset.UtcNow, null);

        static async IAsyncEnumerable<OntologyEvent> CreateStream(OntologyEvent evt)
        {
            yield return evt;
            await Task.CompletedTask;
        }

        provider.QueryEventsAsync(query, Arg.Any<CancellationToken>())
            .Returns(CreateStream(expected));

        // Act
        var events = new List<OntologyEvent>();
        await foreach (var evt in provider.QueryEventsAsync(query, CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        await Assert.That(events).HasCount().EqualTo(1);
        await Assert.That(events[0].Domain).IsEqualTo("CRM");
    }

    [Test]
    public async Task IOntologyProjection_InterfaceExists()
    {
        // Arrange & Act
        var type = typeof(IOntologyProjection);

        // Assert
        await Assert.That(type.IsInterface).IsTrue();
    }
}
