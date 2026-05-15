using System.Collections.Immutable;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Diagnostics;
using Strategos.Ontology.Merge;

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
    /// <remarks>
    /// DR-6 + Task 21: when an existing descriptor already occupies the
    /// incoming <c>(DomainName, Name)</c> slot with opposite provenance
    /// (one hand, one ingested), the two are folded through
    /// <see cref="MergeTwo.Merge"/> in place. This is what makes
    /// hand-authored intent and mechanical ingest meet without tripping
    /// AONT040 on duplicate names. Same-provenance collisions continue
    /// to surface as duplicates (the dup-name diagnostic handles that
    /// case downstream).
    /// </remarks>
    public void ObjectTypeFromDescriptor(ObjectTypeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        EnsureIdentityInvariant(descriptor);

        var existingIdx = FindObjectTypeIndex(descriptor.DomainName, descriptor.Name);
        if (existingIdx >= 0
            && TryCrossProvenanceMerge(_objectTypes[existingIdx], descriptor, out var merged))
        {
            _objectTypes[existingIdx] = merged;
            return;
        }

        _objectTypes.Add(descriptor);
    }

    /// <summary>
    /// Attempts the DR-6 cross-provenance fold (hand ↔ ingested), returning
    /// the merged descriptor when applicable. Returns <c>false</c> for
    /// same-provenance pairs so the caller can choose between "duplicate
    /// (surfaces AONT040 downstream)" semantics (add path) and "replace
    /// at index" semantics (update path).
    /// </summary>
    private static bool TryCrossProvenanceMerge(
        ObjectTypeDescriptor existing,
        ObjectTypeDescriptor incoming,
        out ObjectTypeDescriptor merged)
    {
        if (existing.Source == DescriptorSource.HandAuthored
            && incoming.Source == DescriptorSource.Ingested)
        {
            merged = MergeTwo.Merge(hand: existing, ingested: incoming);
            return true;
        }

        if (existing.Source == DescriptorSource.Ingested
            && incoming.Source == DescriptorSource.HandAuthored)
        {
            merged = MergeTwo.Merge(hand: incoming, ingested: existing);
            return true;
        }

        merged = existing;
        return false;
    }

    /// <summary>
    /// DR-1 identity invariant boundary check. The same invariant is
    /// enforced inside <see cref="ObjectTypeDescriptor"/>'s
    /// <c>ClrType</c>/<c>SymbolKey</c> init setters; this guards the
    /// "neither field set in the object initializer" bypass — both
    /// setters skipped, default-null values land on the descriptor, and
    /// neither setter's invariant runs.
    /// </summary>
    private static void EnsureIdentityInvariant(ObjectTypeDescriptor descriptor)
    {
        if (descriptor.ClrType is null && descriptor.SymbolKey is null)
        {
            throw new InvalidOperationException(
                $"ObjectTypeDescriptor '{descriptor.DomainName}.{descriptor.Name}' "
                + "violates the DR-1 identity invariant: at least one of ClrType or "
                + "SymbolKey must be non-null. Hand-authored descriptors must supply "
                + "ClrType; ingested descriptors must supply SymbolKey.");
        }
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
        EnsureIdentityInvariant(d);

        var idx = FindObjectTypeIndex(d.DomainName, d.Name);
        if (idx < 0)
        {
            // Treat as add if not present — keeps the surface forgiving
            // for sources that emit Update without a prior Add (e.g. a
            // restart that lost partial state). Idempotent.
            _objectTypes.Add(d);
            return;
        }

        // DR-6 lateral lattice: an Update arriving from one provenance
        // against an existing descriptor of the opposite provenance is
        // folded through MergeTwo so neither origin silently overwrites
        // the other. Same-provenance Updates replace at the existing
        // index (no duplicate created, unlike the Add path which lets
        // AONT040 surface duplicates downstream).
        _objectTypes[idx] = TryCrossProvenanceMerge(_objectTypes[idx], d, out var merged)
            ? merged
            : d;
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
