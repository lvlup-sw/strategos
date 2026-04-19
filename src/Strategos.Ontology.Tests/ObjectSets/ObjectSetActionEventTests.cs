using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetActionEventTests
{
    private IObjectSetProvider _provider = null!;
    private IActionDispatcher _dispatcher = null!;
    private IEventStreamProvider _eventProvider = null!;

    [Before(Test)]
    public Task Setup()
    {
        _provider = Substitute.For<IObjectSetProvider>();
        _dispatcher = Substitute.For<IActionDispatcher>();
        _eventProvider = Substitute.For<IEventStreamProvider>();
        return Task.CompletedTask;
    }

    [Test]
    public async Task ObjectSet_ApplyAsync_DelegatesToActionDispatcher()
    {
        // Arrange
        // ExecuteAsync returns items so ApplyAsync knows which objects to dispatch against
        var items = new List<string> { "obj-1" };
        _provider.ExecuteAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ObjectSetResult<string>(items, 1, ObjectSetInclusion.Properties)));
        _dispatcher.DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(true)));
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var results = await set.ApplyAsync("DoSomething", new { Value = 1 });

        // Assert
        await Assert.That(results).Count().IsEqualTo(1);
        await Assert.That(results[0].IsSuccess).IsTrue();
    }

    [Test]
    public async Task ObjectSet_ApplyAsync_PassesActionNameAndRequest()
    {
        // Arrange
        var items = new List<string> { "obj-1" };
        _provider.ExecuteAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ObjectSetResult<string>(items, 1, ObjectSetInclusion.Properties)));
        _dispatcher.DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(true)));
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);
        var request = new { To = "test@example.com" };

        // Act
        await set.ApplyAsync("SendEmail", request);

        // Assert
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(c => c.ActionName == "SendEmail"),
            Arg.Is<object>(r => r == request),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ObjectSet_EventsAsync_DelegatesToEventStreamProvider()
    {
        // Arrange
        var evt = new OntologyEvent("CRM", "Contact", "c-1", "Created", DateTimeOffset.UtcNow, null);

        _eventProvider.QueryEventsAsync(Arg.Any<EventQuery>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(evt));
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var events = new List<OntologyEvent>();
        await foreach (var e in set.EventsAsync())
        {
            events.Add(e);
        }

        // Assert
        await Assert.That(events).Count().IsEqualTo(1);
        await Assert.That(events[0].EventType).IsEqualTo("Created");
    }

    [Test]
    public async Task ObjectSet_EventsAsync_PassesSinceAndEventTypes()
    {
        // Arrange
        var since = TimeSpan.FromHours(1);
        var eventTypes = new List<string> { "Created", "Updated" };

        _eventProvider.QueryEventsAsync(Arg.Any<EventQuery>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<OntologyEvent>());
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        await foreach (var _ in set.EventsAsync(since, eventTypes))
        {
        }

        // Assert
        _eventProvider.Received(1).QueryEventsAsync(
            Arg.Is<EventQuery>(q => q.EventTypes != null && q.EventTypes.Count == 2 && q.Since != null),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Regression: descriptor-name dispatch — ApplyAsync and EventsAsync must
    // route against the descriptor name carried on the expression root, not
    // typeof(T).Name. Guards against the bug CodeRabbit flagged on PR #34
    // where multi-registered CLR types dispatched under the wrong descriptor.
    // -----------------------------------------------------------------------

    [Test]
    public async Task ObjectSet_ApplyAsync_RoutesAgainstDescriptorName_NotClrTypeName()
    {
        // Arrange — construct with an explicit descriptor name that differs from typeof(T).Name
        const string descriptorName = "trading_documents";
        var items = new List<string> { "doc-1" };
        _provider.ExecuteAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ObjectSetResult<string>(items, 1, ObjectSetInclusion.Properties)));
        _dispatcher.DispatchAsync(Arg.Any<ActionContext>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ActionResult(true)));
        var set = new ObjectSet<string>(descriptorName, _provider, _dispatcher, _eventProvider);

        // Act
        await set.ApplyAsync("Archive", new { });

        // Assert — dispatcher receives the descriptor name, not "String"
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<ActionContext>(c => c.ObjectType == descriptorName),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ObjectSet_EventsAsync_RoutesAgainstDescriptorName_NotClrTypeName()
    {
        // Arrange
        const string descriptorName = "trading_documents";
        _eventProvider.QueryEventsAsync(Arg.Any<EventQuery>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<OntologyEvent>());
        var set = new ObjectSet<string>(descriptorName, _provider, _dispatcher, _eventProvider);

        // Act
        await foreach (var _ in set.EventsAsync())
        {
        }

        // Assert — event query carries the descriptor name, not "String"
        _eventProvider.Received(1).QueryEventsAsync(
            Arg.Is<EventQuery>(q => q.ObjectTypeName == descriptorName),
            Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
