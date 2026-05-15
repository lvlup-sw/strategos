using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Strategos.Ontology.Actions;
using Strategos.Ontology.Embeddings;
using Strategos.Ontology.Events;
using Strategos.Ontology.Extensions;
using Strategos.Ontology.ObjectSets;

namespace Strategos.Ontology.Configuration;

/// <summary>
/// Configuration options for registering ontology domains and providers.
/// </summary>
public sealed class OntologyOptions
{
    private readonly List<DomainOntology> _domains = [];
    private readonly List<WorkflowMetadataBuilder> _workflowMetadata = [];
    private readonly List<Action<IServiceCollection>> _serviceRegistrations = [];
    private readonly List<(int Order, Func<IServiceProvider, IActionDispatcher, IActionDispatcher> Factory)> _dispatcherDecorators = [];
    private readonly List<Func<IOntologySource>> _sourceFactories = [];

    internal IReadOnlyList<DomainOntology> Domains => _domains;

    internal IReadOnlyList<WorkflowMetadataBuilder> WorkflowMetadata => _workflowMetadata;

    internal IReadOnlyList<Action<IServiceCollection>> ServiceRegistrations => _serviceRegistrations;

    internal List<(int Order, Func<IServiceProvider, IActionDispatcher, IActionDispatcher> Factory)> DispatcherDecorators => _dispatcherDecorators;

    /// <summary>
    /// DR-3 (Task 12): factories that activate each
    /// <see cref="IOntologySource"/> implementation registered via
    /// <see cref="AddSource{T}"/>. Surfaced to <c>AddOntology</c> so the
    /// graph builder's source-drain instantiates them prior to
    /// <c>Build()</c>. Each factory closes over the generic type
    /// parameter so the trim/AOT annotation propagates correctly without
    /// requiring downstream call sites to thread the annotation through
    /// non-generic API boundaries.
    /// </summary>
    internal IReadOnlyList<Func<IOntologySource>> SourceFactories => _sourceFactories;

    public OntologyOptions AddDomain<T>()
        where T : DomainOntology, new()
    {
        _domains.Add(new T());
        return this;
    }

    public OntologyOptions UseObjectSetProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class, IObjectSetProvider
    {
        _serviceRegistrations.Add(services => services.AddSingleton<IObjectSetProvider, T>());
        return this;
    }

    public OntologyOptions UseEventStreamProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class, IEventStreamProvider
    {
        _serviceRegistrations.Add(services => services.AddSingleton<IEventStreamProvider, T>());
        return this;
    }

    public OntologyOptions UseActionDispatcher<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class, IActionDispatcher
    {
        _serviceRegistrations.Add(services => services.AddSingleton<IActionDispatcher, T>());
        return this;
    }

    public OntologyOptions UseEmbeddingProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class, IEmbeddingProvider
    {
        _serviceRegistrations.Add(services => services.AddSingleton<IEmbeddingProvider, T>());
        return this;
    }

    public OntologyOptions UseObjectSetWriter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class, IObjectSetWriter
    {
        _serviceRegistrations.Add(services => services.AddSingleton<IObjectSetWriter, T>());
        return this;
    }

    /// <summary>
    /// Adds a service registration callback to be applied during DI setup.
    /// Used by extension packages (e.g., Npgsql) to register their services.
    /// </summary>
    /// <param name="registration">The service registration action.</param>
    public OntologyOptions AddServiceRegistration(Action<IServiceCollection> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        _serviceRegistrations.Add(registration);
        return this;
    }

    /// <summary>
    /// DR-3 (Task 12): registers an <see cref="IOntologySource"/>
    /// implementation as transient in the service container and surfaces
    /// it to the <see cref="OntologyGraphBuilder"/> source-drain.
    /// </summary>
    /// <remarks>
    /// Routes through the same <c>graphBuilder.AddSources(...)</c> surface
    /// the in-memory test wiring uses — there is no parallel registration
    /// path. The transient lifetime matches the contract described on
    /// <see cref="IOntologySource"/>: each <c>LoadAsync</c> drain creates
    /// a fresh source instance.
    /// </remarks>
    public OntologyOptions AddSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
        where T : class, IOntologySource, new()
    {
        // Factory closes over the generic parameter — the `new()` constraint
        // gives us a trim-safe activator without resorting to reflection.
        _sourceFactories.Add(static () => new T());
        _serviceRegistrations.Add(services => services.AddTransient<IOntologySource, T>());
        return this;
    }

    public OntologyOptions AddWorkflow(string workflowName, Action<WorkflowMetadataBuilder> configure)
    {
        var builder = new WorkflowMetadataBuilder(workflowName);
        configure(builder);
        _workflowMetadata.Add(builder);
        return this;
    }
}
