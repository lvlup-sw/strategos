using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Events;
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

        // Register IOntologyQuery as a factory so that IObjectSetProvider/IActionDispatcher/
        // IEventStreamProvider — registered later via OntologyOptions service registrations —
        // can be resolved at activation time. When backing services are not registered,
        // falls back to the read-only constructor; GetObjectSet<T> will then throw with a
        // clear diagnostic if invoked without those dependencies.
        services.AddSingleton<IOntologyQuery>(sp =>
        {
            var graphInstance = sp.GetRequiredService<OntologyGraph>();
            var objectSetProvider = sp.GetService<IObjectSetProvider>();
            var actionDispatcher = sp.GetService<IActionDispatcher>();
            var eventStreamProvider = sp.GetService<IEventStreamProvider>();

            return objectSetProvider is not null
                && actionDispatcher is not null
                && eventStreamProvider is not null
                    ? new OntologyQueryService(graphInstance, objectSetProvider, actionDispatcher, eventStreamProvider)
                    : new OntologyQueryService(graphInstance);
        });

        foreach (var registration in options.ServiceRegistrations)
        {
            registration(services);
        }

        ApplyDispatcherDecorators(services, options);

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

    private static void ApplyDispatcherDecorators(IServiceCollection services, OntologyOptions options)
    {
        if (options.DispatcherDecorators.Count == 0)
        {
            return;
        }

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(IActionDispatcher));
        if (descriptor is null)
        {
            return;
        }

        services.Remove(descriptor);

        Func<IServiceProvider, IActionDispatcher> innerFactory;
        if (descriptor.ImplementationType is { } implType)
        {
            // Preserve the original lifetime so callers that registered the
            // dispatcher as Scoped or Transient don't have it silently
            // promoted to Singleton when decorators are wrapped around it.
            services.Add(new ServiceDescriptor(implType, implType, descriptor.Lifetime));
            innerFactory = sp => (IActionDispatcher)sp.GetRequiredService(implType);
        }
        else if (descriptor.ImplementationFactory is { } factory)
        {
            innerFactory = sp => (IActionDispatcher)factory(sp);
        }
        else if (descriptor.ImplementationInstance is IActionDispatcher instance)
        {
            innerFactory = _ => instance;
        }
        else
        {
            // Unsupported descriptor shape — restore original registration.
            services.Add(descriptor);
            return;
        }

        var orderedFactories = options.DispatcherDecorators
            .OrderBy(d => d.Order)
            .Select(d => d.Factory)
            .ToArray();

        // Honor the original IActionDispatcher lifetime when registering the
        // decorated dispatcher — using AddSingleton unconditionally would
        // resolve a Scoped inner from the root scope and create a captive
        // dependency.
        services.Add(new ServiceDescriptor(
            typeof(IActionDispatcher),
            sp =>
            {
                var dispatcher = innerFactory(sp);
                foreach (var factory in orderedFactories)
                {
                    dispatcher = factory(sp, dispatcher);
                }

                return dispatcher;
            },
            descriptor.Lifetime));
    }
}
