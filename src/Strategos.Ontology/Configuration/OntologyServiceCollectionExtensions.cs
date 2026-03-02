using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.ObjectSets;
using Strategos.Ontology.Query;

namespace Strategos.Ontology.Configuration;

/// <summary>
/// Extension methods for registering ontology services in a DI container.
/// </summary>
public static class OntologyServiceCollectionExtensions
{
    /// <summary>
    /// Registers ontology domains, builds the immutable OntologyGraph, and registers
    /// provider/dispatcher implementations in the service collection.
    /// </summary>
    public static IServiceCollection AddOntology(
        this IServiceCollection services,
        Action<OntologyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OntologyOptions();
        configure(options);

        var graphBuilder = new OntologyGraphBuilder();

        foreach (var domain in options.Domains)
        {
            graphBuilder.AddDomain(domain);
        }

        graphBuilder.AddWorkflowMetadata(options.WorkflowMetadata);

        var graph = graphBuilder.Build();
        services.AddSingleton(graph);
        services.AddSingleton<IOntologyQuery>(new OntologyQueryService(graph));

        foreach (var registration in options.ServiceRegistrations)
        {
            registration(services);
        }

        // Auto-detect: if the registered IObjectSetProvider also implements IObjectSetWriter,
        // register IObjectSetWriter to resolve to the same instance.
        var providerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IObjectSetProvider));
        if (providerDescriptor is not null && typeof(IObjectSetWriter).IsAssignableFrom(providerDescriptor.ImplementationType))
        {
            if (!services.Any(d => d.ServiceType == typeof(IObjectSetWriter)))
            {
                services.AddSingleton<IObjectSetWriter>(sp => (IObjectSetWriter)sp.GetRequiredService<IObjectSetProvider>());
            }
        }

        return services;
    }
}
