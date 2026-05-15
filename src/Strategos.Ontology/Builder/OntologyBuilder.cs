using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Diagnostics;

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
                ValidateIngestedIntentInvariant(add.Descriptor);
                ObjectTypeFromDescriptor(add.Descriptor);
                break;

            case OntologyDelta.UpdateObjectType update:
                ValidateIngestedIntentInvariant(update.Descriptor);
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

    /// <summary>
    /// DR-6 + DR-10 (Task 16): AONT205 invariant — a mechanical ingester
    /// (descriptor with <see cref="DescriptorSource.Ingested"/>) cannot
    /// contribute to the intent-only fields <see cref="ObjectTypeDescriptor.Actions"/>,
    /// <see cref="ObjectTypeDescriptor.Events"/>, or
    /// <see cref="ObjectTypeDescriptor.Lifecycle"/>. Hand-authored
    /// descriptors pass through unchanged. On violation, throws
    /// <see cref="OntologyCompositionException"/> with an AONT205
    /// diagnostic naming the offending field, the domain, and the type.
    /// </summary>
    private static void ValidateIngestedIntentInvariant(ObjectTypeDescriptor descriptor)
    {
        if (descriptor.Source != DescriptorSource.Ingested)
        {
            return;
        }

        string? offendingField = null;
        if (descriptor.Actions.Count > 0)
        {
            offendingField = "Actions";
        }
        else if (descriptor.Events.Count > 0)
        {
            offendingField = "Events";
        }
        else if (descriptor.Lifecycle is not null)
        {
            offendingField = "Lifecycle";
        }

        if (offendingField is null)
        {
            return;
        }

        var diagnostic = new OntologyDiagnostic(
            Id: "AONT205",
            Message:
                $"AONT205: ingested descriptor '{descriptor.DomainName}.{descriptor.Name}' "
                + $"contributes to intent-only field '{offendingField}'. "
                + $"Mechanical ingesters (Source = Ingested, SourceId = '{descriptor.SourceId ?? "<unknown>"}') "
                + $"must leave Actions, Events, and Lifecycle empty — those are hand-authored intent.",
            Severity: OntologyDiagnosticSeverity.Error,
            DomainName: descriptor.DomainName,
            TypeName: descriptor.Name,
            PropertyName: offendingField);

        throw new OntologyCompositionException(ImmutableArray.Create(diagnostic));
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
