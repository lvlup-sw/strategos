using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Tests.Configuration;

public class StubEmbeddingProviderForDI : IEmbeddingProvider
{
    public int Dimensions => 384;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(new float[384]);

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[384]).ToList());
}

public class StubObjectSetWriterForDI : IObjectSetWriter
{
    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class =>
        Task.CompletedTask;

    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class =>
        Task.CompletedTask;
}

public class DualProvider : IObjectSetProvider, IObjectSetWriter
{
    public Task<ObjectSetResult<T>> ExecuteAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        Task.FromResult(new ObjectSetResult<T>([], 0, ObjectSetInclusion.Properties));

    public IAsyncEnumerable<T> StreamAsync<T>(ObjectSetExpression expression, CancellationToken ct = default)
        where T : class =>
        AsyncEnumerable.Empty<T>();

    public Task<ScoredObjectSetResult<T>> ExecuteSimilarityAsync<T>(
        SimilarityExpression expression, CancellationToken ct = default) where T : class =>
        Task.FromResult(new ScoredObjectSetResult<T>([], 0, ObjectSetInclusion.Properties, []));

    public Task StoreAsync<T>(T item, CancellationToken ct = default) where T : class =>
        Task.CompletedTask;

    public Task StoreBatchAsync<T>(IReadOnlyList<T> items, CancellationToken ct = default) where T : class =>
        Task.CompletedTask;
}

public class EmbeddingProviderRegistrationTests
{
    [Test]
    public async Task UseEmbeddingProvider_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseEmbeddingProvider<StubEmbeddingProviderForDI>();
        });

        var provider = services.BuildServiceProvider();

        // Act
        var embeddingProvider = provider.GetService<IEmbeddingProvider>();

        // Assert
        await Assert.That(embeddingProvider).IsNotNull();
        await Assert.That(embeddingProvider).IsTypeOf<StubEmbeddingProviderForDI>();
    }

    [Test]
    public async Task UseObjectSetWriter_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseObjectSetWriter<StubObjectSetWriterForDI>();
        });

        var provider = services.BuildServiceProvider();

        // Act
        var writer = provider.GetService<IObjectSetWriter>();

        // Assert
        await Assert.That(writer).IsNotNull();
        await Assert.That(writer).IsTypeOf<StubObjectSetWriterForDI>();
    }

    [Test]
    public async Task UseObjectSetProvider_WhenAlsoWriter_RegistersBothInterfaces()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddOntology(options =>
        {
            options.AddDomain<TestOntologyDomain>();
            options.UseObjectSetProvider<DualProvider>();
        });

        var provider = services.BuildServiceProvider();

        // Act
        var objectSetProvider = provider.GetService<IObjectSetProvider>();
        var writer = provider.GetService<IObjectSetWriter>();

        // Assert
        await Assert.That(objectSetProvider).IsNotNull();
        await Assert.That(objectSetProvider).IsTypeOf<DualProvider>();
        await Assert.That(writer).IsNotNull();
        await Assert.That(writer).IsTypeOf<DualProvider>();
        // Verify they resolve to the same instance
        await Assert.That(writer).IsSameReferenceAs(objectSetProvider);
    }
}
