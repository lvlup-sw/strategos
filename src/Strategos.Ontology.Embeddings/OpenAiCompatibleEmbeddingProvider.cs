using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Strategos.Ontology.Embeddings;

namespace Strategos.Ontology.Embeddings;

/// <summary>
/// An embedding provider that uses OpenAI-compatible HTTP APIs to generate text embeddings.
/// </summary>
public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiEmbeddingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiCompatibleEmbeddingProvider"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for API calls.</param>
    /// <param name="options">The configuration options.</param>
    public OpenAiCompatibleEmbeddingProvider(HttpClient httpClient, IOptions<OpenAiEmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public int Dimensions => _options.Dimensions;

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync([text], cancellationToken).ConfigureAwait(false);
        return results[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var allResults = new float[texts.Count][];
        var batchSize = _options.BatchSize;
        var offset = 0;

        while (offset < texts.Count)
        {
            var batchCount = Math.Min(batchSize, texts.Count - offset);
            var batch = new string[batchCount];
            for (var i = 0; i < batchCount; i++)
            {
                batch[i] = texts[offset + i];
            }

            var embeddings = await SendBatchRequestAsync(batch, cancellationToken).ConfigureAwait(false);

            // Order by index and place into the correct positions
            foreach (var item in embeddings)
            {
                allResults[offset + item.Index] = item.Embedding;
            }

            offset += batchCount;
        }

        return allResults;
    }

    private async Task<EmbeddingData[]> SendBatchRequestAsync(
        string[] inputs,
        CancellationToken cancellationToken)
    {
        var requestBody = new EmbeddingRequest
        {
            Model = _options.Model,
            Input = inputs,
        };

        var endpoint = _options.Endpoint.TrimEnd('/');
        var requestUri = $"{endpoint}/embeddings";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(requestBody, EmbeddingsJsonContext.Default.EmbeddingRequest);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Embedding API request failed with status {(int)response.StatusCode}: {errorBody}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var result = await response.Content
            .ReadFromJsonAsync(EmbeddingsJsonContext.Default.EmbeddingResponse, cancellationToken)
            .ConfigureAwait(false);

        return result?.Data ?? [];
    }
}

[JsonSerializable(typeof(EmbeddingRequest))]
[JsonSerializable(typeof(EmbeddingResponse))]
internal sealed partial class EmbeddingsJsonContext : JsonSerializerContext
{
}

internal sealed class EmbeddingRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required string[] Input { get; init; }
}

internal sealed class EmbeddingResponse
{
    [JsonPropertyName("data")]
    public EmbeddingData[] Data { get; init; } = [];
}

internal sealed class EmbeddingData
{
    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = [];

    [JsonPropertyName("index")]
    public int Index { get; init; }
}
