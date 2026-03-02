using Strategos.Ontology.Embeddings;

namespace Strategos.Ontology.Tests.Embeddings;

public class StubEmbeddingProvider : IEmbeddingProvider
{
    private readonly int _dimensions;
    private readonly float[] _embedding;

    public StubEmbeddingProvider(int dimensions = 384)
    {
        _dimensions = dimensions;
        _embedding = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
            _embedding[i] = i * 0.01f;
    }

    public int Dimensions => _dimensions;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(_embedding);

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => _embedding).ToList());
}

public class IEmbeddingProviderTests
{
    [Test]
    public async Task EmbedAsync_ReturnsFloatArray_ImplementationCanBeCalled()
    {
        // Arrange
        var provider = new StubEmbeddingProvider(dimensions: 128);

        // Act
        var result = await provider.EmbedAsync("hello world");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).HasCount().EqualTo(128);
        await Assert.That(result).IsTypeOf<float[]>();
    }

    [Test]
    public async Task EmbedBatchAsync_ReturnsReadOnlyList_ImplementationCanBeCalled()
    {
        // Arrange
        var provider = new StubEmbeddingProvider(dimensions: 64);
        var texts = new List<string> { "text one", "text two", "text three" };

        // Act
        var result = await provider.EmbedBatchAsync(texts);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).HasCount().EqualTo(3);
        await Assert.That(result[0]).HasCount().EqualTo(64);
    }

    [Test]
    public async Task Dimensions_ReturnsConfiguredValue()
    {
        // Arrange
        var provider = new StubEmbeddingProvider(dimensions: 1536);

        // Act
        var dimensions = provider.Dimensions;

        // Assert
        await Assert.That(dimensions).IsEqualTo(1536);
    }
}
