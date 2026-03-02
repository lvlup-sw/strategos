using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Npgsql;

/// <summary>
/// Extension methods for registering pgvector object set services.
/// </summary>
public static class PgVectorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the pgvector-backed object set provider and writer in the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">Action to configure <see cref="PgVectorOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode("PgVectorObjectSetProvider uses JSON serialization which requires dynamic code.")]
    [RequiresUnreferencedCode("PgVectorObjectSetProvider uses JSON serialization which requires unreferenced code.")]
    public static IServiceCollection AddPgVectorObjectSets(
        this IServiceCollection services, Action<PgVectorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddSingleton<PgVectorObjectSetProvider>();
        services.AddSingleton<IObjectSetProvider>(sp => sp.GetRequiredService<PgVectorObjectSetProvider>());
        services.AddSingleton<IObjectSetWriter>(sp => sp.GetRequiredService<PgVectorObjectSetProvider>());

        return services;
    }
}
