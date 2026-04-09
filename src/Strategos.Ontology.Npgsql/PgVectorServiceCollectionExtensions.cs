using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Strategos.Ontology.Configuration;
using Strategos.Ontology.Embeddings;
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
        services.AddSingleton<PgVectorObjectSetProvider>(sp => CreateProvider(sp));
        services.AddSingleton<IObjectSetProvider>(sp => sp.GetRequiredService<PgVectorObjectSetProvider>());
        services.AddSingleton<IObjectSetWriter>(sp => sp.GetRequiredService<PgVectorObjectSetProvider>());

        return services;
    }

    /// <summary>
    /// Shorthand to register pgvector-backed object sets using a connection string.
    /// Configures <see cref="NpgsqlDataSource"/>, <see cref="IObjectSetProvider"/>, and <see cref="IObjectSetWriter"/>.
    /// </summary>
    /// <param name="options">The ontology options being configured.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The ontology options for chaining.</returns>
    [RequiresDynamicCode("PgVectorObjectSetProvider uses JSON serialization which requires dynamic code.")]
    [RequiresUnreferencedCode("PgVectorObjectSetProvider uses JSON serialization which requires unreferenced code.")]
    public static OntologyOptions UsePgVector(this OntologyOptions options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        options.AddServiceRegistration(services =>
        {
            services.Configure<PgVectorOptions>(opts => opts.ConnectionString = connectionString);

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            services.AddSingleton(dataSourceBuilder.Build());

            services.AddSingleton<PgVectorObjectSetProvider>(sp => CreateProvider(sp));
            services.AddSingleton<IObjectSetProvider>(sp => sp.GetRequiredService<PgVectorObjectSetProvider>());
            services.AddSingleton<IObjectSetWriter>(sp => sp.GetRequiredService<PgVectorObjectSetProvider>());
        });

        return options;
    }

    /// <summary>
    /// Activation factory that resolves the required dependencies and passes the
    /// optional <see cref="OntologyGraph"/> through to the provider constructor
    /// so the default-named write overloads can honour graph-registered
    /// descriptor names (F4 / bug #31).
    /// </summary>
    [RequiresDynamicCode("PgVectorObjectSetProvider uses JSON serialization which requires dynamic code.")]
    [RequiresUnreferencedCode("PgVectorObjectSetProvider uses JSON serialization which requires unreferenced code.")]
    private static PgVectorObjectSetProvider CreateProvider(IServiceProvider sp)
    {
        var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
        var embeddingProvider = sp.GetRequiredService<IEmbeddingProvider>();
        var providerOptions = sp.GetRequiredService<IOptions<PgVectorOptions>>();
        var logger = sp.GetRequiredService<ILogger<PgVectorObjectSetProvider>>();
        var graph = sp.GetService<OntologyGraph>();
        return new PgVectorObjectSetProvider(dataSource, embeddingProvider, providerOptions, logger, graph);
    }
}
