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
    /// DR-5 (Tasks 10 + 11): dispatches an <see cref="OntologyDelta"/>
    /// by variant. The <see cref="OntologyDelta.AddObjectType"/> branch
    /// routes to <see cref="ObjectTypeFromDescriptor"/>; the remaining
    /// seven variants are handled inline below. The default arm throws
    /// <see cref="NotSupportedException"/> naming the offending variant
    /// type (AC2: explicit fallthrough).
    /// </summary>
    public void ApplyDelta(OntologyDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        switch (delta)
        {
            case OntologyDelta.AddObjectType add:
                ObjectTypeFromDescriptor(add.Descriptor);
                break;

            case OntologyDelta.UpdateObjectType update:
                ApplyUpdateObjectType(update);
                break;

            case OntologyDelta.RemoveObjectType remove:
                ApplyRemoveObjectType(remove);
                break;

            case OntologyDelta.AddProperty addProp:
                ApplyAddProperty(addProp);
                break;

            case OntologyDelta.RenameProperty rename:
                ApplyRenameProperty(rename);
                break;

            case OntologyDelta.RemoveProperty removeProp:
                ApplyRemoveProperty(removeProp);
                break;

            case OntologyDelta.AddLink addLink:
                ApplyAddLink(addLink);
                break;

            case OntologyDelta.RemoveLink removeLink:
                ApplyRemoveLink(removeLink);
                break;

            default:
                throw new NotSupportedException(
                    $"Unknown delta variant: {delta.GetType().Name}");
        }
    }

    private void ApplyUpdateObjectType(OntologyDelta.UpdateObjectType delta)
    {
        var d = delta.Descriptor;
        var idx = FindObjectTypeIndex(d.DomainName, d.Name);
        if (idx < 0)
        {
            // Treat as add if not present — keeps the surface forgiving
            // for sources that emit Update without a prior Add (e.g. a
            // restart that lost partial state). Idempotent.
            _objectTypes.Add(d);
            return;
        }

        _objectTypes[idx] = d;
    }

    private void ApplyRemoveObjectType(OntologyDelta.RemoveObjectType delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.TypeName);
        if (idx >= 0)
        {
            _objectTypes.RemoveAt(idx);
        }
    }

    private void ApplyAddProperty(OntologyDelta.AddProperty delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.TypeName);
        if (idx < 0)
        {
            return;
        }

        var current = _objectTypes[idx];
        var updated = current.Properties.ToList();
        updated.Add(delta.Descriptor);
        _objectTypes[idx] = current with { Properties = updated.AsReadOnly() };
    }

    private void ApplyRenameProperty(OntologyDelta.RenameProperty delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.TypeName);
        if (idx < 0)
        {
            return;
        }

        var current = _objectTypes[idx];
        var updated = current.Properties.ToList();
        for (var i = 0; i < updated.Count; i++)
        {
            if (updated[i].Name == delta.FromName)
            {
                updated[i] = updated[i] with { Name = delta.ToName };
            }
        }

        _objectTypes[idx] = current with { Properties = updated.AsReadOnly() };
    }

    private void ApplyRemoveProperty(OntologyDelta.RemoveProperty delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.TypeName);
        if (idx < 0)
        {
            return;
        }

        var current = _objectTypes[idx];
        var updated = current.Properties.Where(p => p.Name != delta.PropertyName).ToList();
        _objectTypes[idx] = current with { Properties = updated.AsReadOnly() };
    }

    private void ApplyAddLink(OntologyDelta.AddLink delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.SourceTypeName);
        if (idx < 0)
        {
            return;
        }

        var current = _objectTypes[idx];
        var updated = current.Links.ToList();
        updated.Add(delta.Descriptor);
        _objectTypes[idx] = current with { Links = updated.AsReadOnly() };
    }

    private void ApplyRemoveLink(OntologyDelta.RemoveLink delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.SourceTypeName);
        if (idx < 0)
        {
            return;
        }

        var current = _objectTypes[idx];
        var updated = current.Links.Where(l => l.Name != delta.LinkName).ToList();
        _objectTypes[idx] = current with { Links = updated.AsReadOnly() };
    }

    private int FindObjectTypeIndex(string domainName, string typeName)
    {
        for (var i = 0; i < _objectTypes.Count; i++)
        {
            var ot = _objectTypes[i];
            if (ot.DomainName == domainName && ot.Name == typeName)
            {
                return i;
            }
        }

        return -1;
    }
}
