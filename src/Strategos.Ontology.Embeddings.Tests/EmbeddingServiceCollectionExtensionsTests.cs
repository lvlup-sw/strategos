using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Strategos.Ontology.Embeddings;

namespace Strategos.Ontology.Embeddings.Tests;

public class EmbeddingServiceCollectionExtensionsTests
{
    [Test]
    public async Task AddOpenAiEmbeddings_RegistersIEmbeddingProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOpenAiEmbeddings(o =>
        {
            o.Endpoint = "https://api.openai.com/v1";
            o.ApiKey = "test-key";
        });

        var provider = services.BuildServiceProvider();

        // Act
        var embeddingProvider = provider.GetService<IEmbeddingProvider>();

        // Assert
        await Assert.That(embeddingProvider).IsNotNull();
        await Assert.That(embeddingProvider).IsTypeOf<OpenAiCompatibleEmbeddingProvider>();
    }

    [Test]
    public async Task AddOpenAiEmbeddings_ConfiguresOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOpenAiEmbeddings(o =>
        {
            o.Endpoint = "https://custom.api.com/v1";
            o.ApiKey = "my-secret-key";
            o.Model = "text-embedding-ada-002";
            o.Dimensions = 768;
            o.BatchSize = 50;
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<OpenAiEmbeddingOptions>>().Value;

        // Assert
        await Assert.That(options.Endpoint).IsEqualTo("https://custom.api.com/v1");
        await Assert.That(options.ApiKey).IsEqualTo("my-secret-key");
        await Assert.That(options.Model).IsEqualTo("text-embedding-ada-002");
        await Assert.That(options.Dimensions).IsEqualTo(768);
        await Assert.That(options.BatchSize).IsEqualTo(50);
    }
}
