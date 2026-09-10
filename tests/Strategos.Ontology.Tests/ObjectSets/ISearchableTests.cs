using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.ObjectSets;

public class ISearchableTests
{
    [Test]
    public async Task ISearchable_CanBeImplemented()
    {
        // Arrange & Act
        var searchable = new SearchableTestObject(new float[] { 1.0f, 2.0f, 3.0f });

        // Assert
        await Assert.That(searchable).IsNotNull();
        await Assert.That(searchable).IsAssignableTo<ISearchable>();
    }

    [Test]
    public async Task ISearchable_Embedding_ReturnsVector()
    {
        // Arrange
        var expected = new float[] { 0.5f, 0.3f, 0.8f };
        ISearchable searchable = new SearchableTestObject(expected);

        // Act
        var embedding = searchable.Embedding;

        // Assert
        await Assert.That(embedding).HasCount().EqualTo(3);
        await Assert.That(embedding[0]).IsEqualTo(0.5f);
        await Assert.That(embedding[1]).IsEqualTo(0.3f);
        await Assert.That(embedding[2]).IsEqualTo(0.8f);
    }

    [Test]
    public async Task ISearchable_IsInterface()
    {
        // Arrange
        var type = typeof(ISearchable);

        // Act & Assert
        await Assert.That(type.IsInterface).IsTrue();
    }
}

// Test helper implementing ISearchable
internal sealed class SearchableTestObject : ISearchable
{
    public SearchableTestObject(float[] embedding)
    {
        Embedding = embedding;
    }

    public float[] Embedding { get; }
}
