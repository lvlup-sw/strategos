using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Strategos.Ontology.Embeddings;

namespace Strategos.Ontology.Embeddings.Tests;

/// <summary>
/// Tests for the Timeout property on <see cref="OpenAiEmbeddingOptions"/>
/// and its integration with the HTTP client configuration.
/// </summary>
public class OpenAiEmbeddingOptionsTimeoutTests
{
    [Test]
    public async Task DefaultTimeout_Is30Seconds()
    {
        var options = new OpenAiEmbeddingOptions();
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task Timeout_IsConfigurable()
    {
        var options = new OpenAiEmbeddingOptions { Timeout = TimeSpan.FromMinutes(2) };
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
    }

    [Test]
    public async Task AddOpenAiEmbeddings_ConfiguresTimeout()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddOpenAiEmbeddings(o =>
        {
            o.Endpoint = "https://api.openai.com/v1";
            o.ApiKey = "test-key";
            o.Timeout = TimeSpan.FromSeconds(60);
        });

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<OpenAiEmbeddingOptions>>().Value;

        // Assert
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(60));
    }
}
