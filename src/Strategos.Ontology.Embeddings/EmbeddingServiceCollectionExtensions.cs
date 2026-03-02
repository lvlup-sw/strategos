using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Embeddings;

namespace Strategos.Ontology.Embeddings;

/// <summary>
/// Extension methods for registering OpenAI embedding services.
/// </summary>
public static class EmbeddingServiceCollectionExtensions
{
    /// <summary>
    /// Adds an OpenAI-compatible embedding provider to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure the embedding options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenAiEmbeddings(
        this IServiceCollection services,
        Action<OpenAiEmbeddingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddHttpClient<IEmbeddingProvider, OpenAiCompatibleEmbeddingProvider>();
        return services;
    }
}
