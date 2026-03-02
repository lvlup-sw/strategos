// =============================================================================
// <copyright file="RagServiceExtensions.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Strategos.Rag;

/// <summary>
/// Extension methods for registering RAG collection adapters with the DI container.
/// </summary>
[Obsolete("Use OntologyOptions.UseObjectSetProvider<T>() from Strategos.Ontology. See AddOntology().", false)]
public static class RagServiceExtensions
{
    /// <summary>
    /// Registers a RAG collection adapter with the service collection.
    /// </summary>
    /// <typeparam name="TCollection">The collection marker type implementing <see cref="IRagCollection"/>.</typeparam>
    /// <typeparam name="TAdapter">The adapter implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime. Defaults to Singleton.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRagCollection<TCollection, TAdapter>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TCollection : IRagCollection
        where TAdapter : class, IVectorSearchAdapter<TCollection>
    {
        services.Add(new ServiceDescriptor(
            typeof(IVectorSearchAdapter<TCollection>),
            typeof(TAdapter),
            lifetime));
        return services;
    }

    /// <summary>
    /// Registers a RAG collection adapter instance with the service collection.
    /// </summary>
    /// <typeparam name="TCollection">The collection marker type implementing <see cref="IRagCollection"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="adapter">The adapter instance to register.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRagCollection<TCollection>(
        this IServiceCollection services,
        IVectorSearchAdapter<TCollection> adapter)
        where TCollection : IRagCollection
    {
        services.AddSingleton<IVectorSearchAdapter<TCollection>>(adapter);
        return services;
    }
}
