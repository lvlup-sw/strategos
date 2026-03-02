using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class StubObjectSetWriter : IObjectSetWriter
{
    public List<object> StoredItems { get; } = [];

    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class
    {
        StoredItems.Add(item);
        return Task.CompletedTask;
    }

    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class
    {
        StoredItems.AddRange(items);
        return Task.CompletedTask;
    }
}

public class IObjectSetWriterTests
{
    [Test]
    public async Task StoreAsync_ImplementationCanBeCalled()
    {
        // Arrange
        var writer = new StubObjectSetWriter();

        // Act & Assert — no exception thrown
        await writer.StoreAsync("test item");

        await Assert.That(writer.StoredItems).HasCount().EqualTo(1);
        await Assert.That(writer.StoredItems[0]).IsEqualTo("test item");
    }

    [Test]
    public async Task StoreBatchAsync_ImplementationCanBeCalled()
    {
        // Arrange
        var writer = new StubObjectSetWriter();
        var items = new List<string> { "item1", "item2", "item3" };

        // Act & Assert — no exception thrown
        await writer.StoreBatchAsync(items);

        await Assert.That(writer.StoredItems).HasCount().EqualTo(3);
    }
}
