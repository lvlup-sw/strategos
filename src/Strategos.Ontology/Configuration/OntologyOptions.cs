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

    internal IReadOnlyList<DomainOntology> Domains => _domains;

    internal IReadOnlyList<WorkflowMetadataBuilder> WorkflowMetadata => _workflowMetadata;

    internal IReadOnlyList<Action<IServiceCollection>> ServiceRegistrations => _serviceRegistrations;

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

    public OntologyOptions AddWorkflow(string workflowName, Action<WorkflowMetadataBuilder> configure)
    {
        var builder = new WorkflowMetadataBuilder(workflowName);
        configure(builder);
        _workflowMetadata.Add(builder);
        return this;
    }
}
