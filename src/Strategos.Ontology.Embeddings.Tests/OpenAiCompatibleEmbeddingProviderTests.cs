using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Strategos.Ontology.Embeddings.Tests;

public class OpenAiCompatibleEmbeddingProviderTests
{
    private static OpenAiEmbeddingOptions DefaultOptions => new()
    {
        Endpoint = "https://api.openai.com/v1",
        ApiKey = "test-api-key",
        Model = "text-embedding-3-small",
        Dimensions = 1536,
        BatchSize = 100,
    };

    private static HttpResponseMessage CreateEmbeddingResponse(float[][] embeddings)
    {
        var data = embeddings.Select((e, i) => new { embedding = e, index = i }).ToArray();
        var json = JsonSerializer.Serialize(new { data });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static OpenAiCompatibleEmbeddingProvider CreateProvider(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler,
        OpenAiEmbeddingOptions? options = null)
    {
        var opts = options ?? DefaultOptions;
        var httpClient = new HttpClient(new MockHttpMessageHandler(handler));
        return new OpenAiCompatibleEmbeddingProvider(httpClient, Options.Create(opts));
    }

    [Test]
    public async Task EmbedAsync_SingleText_ReturnsFloatArray()
    {
        // Arrange
        var expected = new[] { 0.1f, 0.2f, 0.3f };
        var provider = CreateProvider(_ =>
            Task.FromResult(CreateEmbeddingResponse([expected])));

        // Act
        var result = await provider.EmbedAsync("hello world");

        // Assert
        await Assert.That(result).IsEquivalentTo(expected);
    }

    [Test]
    public async Task EmbedAsync_SendsCorrectRequestBody()
    {
        // Arrange
        string? capturedBody = null;
        string? capturedAuth = null;
        string? capturedUri = null;

        var provider = CreateProvider(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            capturedAuth = request.Headers.Authorization?.ToString();
            capturedUri = request.RequestUri?.ToString();
            return CreateEmbeddingResponse([new[] { 0.1f }]);
        });

        // Act
        await provider.EmbedAsync("test input");

        // Assert
        await Assert.That(capturedBody).IsNotNull();
        var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;

        await Assert.That(root.GetProperty("model").GetString()).IsEqualTo("text-embedding-3-small");
        await Assert.That(root.GetProperty("input").GetArrayLength()).IsEqualTo(1);
        await Assert.That(root.GetProperty("input")[0].GetString()).IsEqualTo("test input");
        await Assert.That(capturedAuth).IsEqualTo("Bearer test-api-key");
        await Assert.That(capturedUri).IsEqualTo("https://api.openai.com/v1/embeddings");
    }

    [Test]
    public async Task EmbedBatchAsync_UnderBatchSize_SingleApiCall()
    {
        // Arrange
        var callCount = 0;
        var provider = CreateProvider(_ =>
        {
            Interlocked.Increment(ref callCount);
            var embeddings = Enumerable.Range(0, 5).Select(_ => new[] { 0.1f }).ToArray();
            return Task.FromResult(CreateEmbeddingResponse(embeddings));
        });

        var texts = Enumerable.Range(0, 5).Select(i => $"text {i}").ToList();

        // Act
        await provider.EmbedBatchAsync(texts);

        // Assert
        await Assert.That(callCount).IsEqualTo(1);
    }

    [Test]
    public async Task EmbedBatchAsync_ExceedsBatchSize_SplitsIntoBatches()
    {
        // Arrange
        var callCount = 0;
        var provider = CreateProvider(async request =>
        {
            Interlocked.Increment(ref callCount);
            var body = await request.Content!.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var inputCount = doc.RootElement.GetProperty("input").GetArrayLength();
            var embeddings = Enumerable.Range(0, inputCount).Select(_ => new[] { 0.1f }).ToArray();
            return CreateEmbeddingResponse(embeddings);
        }, new OpenAiEmbeddingOptions
        {
            Endpoint = "https://api.openai.com/v1",
            ApiKey = "test-api-key",
            BatchSize = 100,
        });

        var texts = Enumerable.Range(0, 150).Select(i => $"text {i}").ToList();

        // Act
        await provider.EmbedBatchAsync(texts);

        // Assert
        await Assert.That(callCount).IsEqualTo(2);
    }

    [Test]
    public async Task EmbedBatchAsync_ReturnsAllEmbeddings_InOrder()
    {
        // Arrange
        var provider = CreateProvider(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var inputs = doc.RootElement.GetProperty("input");
            var embeddings = new float[inputs.GetArrayLength()][];
            for (var i = 0; i < inputs.GetArrayLength(); i++)
            {
                // Use the index as the embedding value so we can verify order
                embeddings[i] = [i * 1.0f];
            }

            return CreateEmbeddingResponse(embeddings);
        });

        var texts = Enumerable.Range(0, 5).Select(i => $"text {i}").ToList();

        // Act
        var results = await provider.EmbedBatchAsync(texts);

        // Assert
        await Assert.That(results.Count).IsEqualTo(5);
        for (var i = 0; i < 5; i++)
        {
            await Assert.That(results[i][0]).IsEqualTo(i * 1.0f);
        }
    }

    [Test]
    public async Task Dimensions_ReturnsConfiguredValue()
    {
        // Arrange
        var provider = CreateProvider(
            _ => Task.FromResult(CreateEmbeddingResponse([[0.1f]])),
            new OpenAiEmbeddingOptions
            {
                Endpoint = "https://api.openai.com/v1",
                ApiKey = "test-key",
                Dimensions = 768,
            });

        // Act & Assert
        await Assert.That(provider.Dimensions).IsEqualTo(768);
    }

    [Test]
    public async Task EmbedAsync_HttpError_ThrowsHttpRequestException()
    {
        // Arrange
        var provider = CreateProvider(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("Unauthorized"),
            }));

        // Act & Assert
        await Assert.That(async () => await provider.EmbedAsync("test")).Throws<HttpRequestException>();
    }

    [Test]
    public async Task EmbedAsync_CustomEndpoint_UsesConfiguredUrl()
    {
        // Arrange
        string? capturedUri = null;
        var provider = CreateProvider(request =>
        {
            capturedUri = request.RequestUri?.ToString();
            return Task.FromResult(CreateEmbeddingResponse([[0.1f]]));
        }, new OpenAiEmbeddingOptions
        {
            Endpoint = "https://my-custom-api.example.com/v2",
            ApiKey = "custom-key",
        });

        // Act
        await provider.EmbedAsync("test");

        // Assert
        await Assert.That(capturedUri).IsEqualTo("https://my-custom-api.example.com/v2/embeddings");
    }
}
