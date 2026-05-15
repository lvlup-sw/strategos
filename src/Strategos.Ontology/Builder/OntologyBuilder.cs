using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Builder;

internal sealed class OntologyBuilder(string domainName) : IOntologyBuilder
{
    private readonly List<ObjectTypeDescriptor> _objectTypes = [];
    private readonly List<InterfaceDescriptor> _interfaces = [];
    private readonly List<CrossDomainLinkBuilder> _crossDomainLinkBuilders = [];

    public IReadOnlyList<ObjectTypeDescriptor> ObjectTypes => _objectTypes.AsReadOnly();

    public IReadOnlyList<InterfaceDescriptor> Interfaces => _interfaces.AsReadOnly();

    public IReadOnlyList<CrossDomainLinkDescriptor> CrossDomainLinks =>
        _crossDomainLinkBuilders.ConvertAll(b => b.Build()).AsReadOnly();

    public void Object<T>(Action<IObjectTypeBuilder<T>> configure)
        where T : class
        => Object<T>(name: null, configure);

    public void Object<T>(string? name, Action<IObjectTypeBuilder<T>> configure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ObjectTypeBuilder<T>(domainName, explicitName: name);
        configure(builder);
        _objectTypes.Add(builder.Build());
    }

    public void Interface<T>(string name, Action<IInterfaceBuilder<T>> configure)
        where T : class
    {
        var builder = new InterfaceBuilder<T>(name);
        configure(builder);
        _interfaces.Add(builder.Build());
    }

    public ICrossDomainLinkBuilder CrossDomainLink(string name)
    {
        var builder = new CrossDomainLinkBuilder(name);
        _crossDomainLinkBuilders.Add(builder);
        return builder;
    }

    /// <summary>
    /// DR-5 (Task 9): registers a fully-specified descriptor without the
    /// expression-tree DSL. Source provenance is preserved so ingested
    /// descriptors flow through to graph-freeze tagged as such.
    /// </summary>
    public void ObjectTypeFromDescriptor(ObjectTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _objectTypes.Add(descriptor);
    }

    /// <summary>
    /// DR-5 (Task 10): dispatches an <see cref="OntologyDelta"/> by
    /// variant. The <see cref="OntologyDelta.AddObjectType"/> branch
    /// routes to <see cref="ObjectTypeFromDescriptor"/>. Task 11 extends
    /// this switch to the remaining variants.
    /// </summary>
    public void ApplyDelta(OntologyDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        switch (delta)
        {
            case OntologyDelta.AddObjectType add:
                ObjectTypeFromDescriptor(add.Descriptor);
                break;

            default:
                throw new NotSupportedException(
                    $"Unknown delta variant: {delta.GetType().Name}");
        }
    }
}
