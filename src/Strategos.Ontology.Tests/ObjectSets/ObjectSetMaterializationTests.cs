using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ObjectSetMaterializationTests
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
    public async Task ObjectSet_ExecuteAsync_DelegatesToProvider()
    {
        // Arrange
        var expected = new ObjectSetResult<string>(["a", "b"], 2, ObjectSetInclusion.Properties);
        _provider.ExecuteAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var result = await set.ExecuteAsync();

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(2);
        await _provider.Received(1).ExecuteAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ObjectSet_StreamAsync_DelegatesToProvider()
    {
        // Arrange
        static async IAsyncEnumerable<string> CreateStream()
        {
            yield return "x";
            yield return "y";
            await Task.CompletedTask;
        }

        _provider.StreamAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(CreateStream());
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider);

        // Act
        var items = new List<string>();
        await foreach (var item in set.StreamAsync())
        {
            items.Add(item);
        }

        // Assert
        await Assert.That(items).Count().IsEqualTo(2);
        _provider.Received(1).StreamAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ObjectSet_ExecuteAsync_PassesExpressionTree()
    {
        // Arrange
        var expected = new ObjectSetResult<string>(["a"], 1, ObjectSetInclusion.Properties);
        _provider.ExecuteAsync<string>(Arg.Any<ObjectSetExpression>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));
        var set = new ObjectSet<string>(typeof(string).Name, _provider, _dispatcher, _eventProvider)
            .Where(s => s.Length > 0);

        // Act
        await set.ExecuteAsync();

        // Assert — verify the expression passed is a FilterExpression
        await _provider.Received(1).ExecuteAsync<string>(
            Arg.Is<ObjectSetExpression>(e => e is FilterExpression),
            Arg.Any<CancellationToken>());
    }
}
