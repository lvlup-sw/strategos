using System.Collections.Immutable;
using System.Collections.ObjectModel;

using Strategos.Ontology.Descriptors;
using Strategos.Ontology.Diagnostics;
using Strategos.Ontology.Merge;

namespace Strategos.Ontology.Builder;

internal sealed class OntologyBuilder(string domainName) : IOntologyBuilder
{
    private readonly List<ObjectTypeDescriptor> _objectTypes = [];
    private readonly List<InterfaceDescriptor> _interfaces = [];
    private readonly List<CrossDomainLinkBuilder> _crossDomainLinkBuilders = [];

    // DR-7 (Tasks 23-30): retain a snapshot of the original ingested
    // descriptor (pre-merge) per (DomainName, Name). The graph-freeze
    // checks need to compare the hand-declared property set against the
    // pre-merge ingested set: by the time MergeTwo has run, hand-only
    // properties have been merged in alongside ingested-only ones, so a
    // direct inspection of the merged descriptor cannot tell which side
    // each property came from "originally". Keyed by
    // (DomainName, Name); only the most recent ingested original is kept
    // when the same (Domain, Name) collides (deltas are
    // last-write-wins on the merged side, this snapshot follows that).
    private readonly Dictionary<(string DomainName, string Name), ObjectTypeDescriptor> _ingestedOriginals = new();

    /// <summary>
    /// DR-7 graph-freeze: pre-merge ingested originals keyed by
    /// (DomainName, Name). Surfaces to <see cref="OntologyGraphBuilder"/>
    /// so AONT201–AONT208 can compare hand vs ingested without losing
    /// provenance after MergeTwo's per-name union.
    /// </summary>
    internal IReadOnlyDictionary<(string DomainName, string Name), ObjectTypeDescriptor> IngestedOriginals
        // Defensive wrapper: callers must not be able to cast back to
        // Dictionary<,> and mutate the snapshot. A fresh ReadOnlyDictionary
        // each access is cheap (graph-freeze reads it once per build).
        => new ReadOnlyDictionary<(string DomainName, string Name), ObjectTypeDescriptor>(_ingestedOriginals);

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

        // DR-7 (Tasks 23-30): snapshot the pre-merge ingested original so
        // graph-freeze diagnostics can compare hand vs ingested property
        // sets after MergeTwo collapses provenance into a single list.
        SyncIngestedOriginal(descriptor);

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
            // No prior descriptor means no cross-provenance fold could
            // have happened: SyncIngestedOriginal applies its normal rule
            // (ingested → store, hand → clear).
            SyncIngestedOriginal(d);
            return;
        }

        // DR-6 lateral lattice: an Update arriving from one provenance
        // against an existing descriptor of the opposite provenance is
        // folded through MergeTwo so neither origin silently overwrites
        // the other. Same-provenance Updates replace at the existing
        // index (no duplicate created, unlike the Add path which lets
        // AONT040 surface duplicates downstream).
        var existing = _objectTypes[idx];
        _objectTypes[idx] = TryCrossProvenanceMerge(existing, d, out var merged)
            ? merged
            : d;

        // DR-7: sync the ingested-original snapshot AFTER the merge
        // decision. When an incoming hand-authored Update folds against
        // an existing ingested baseline, MergeTwo keeps the ingested
        // contributions — so the snapshot must be preserved (not cleared
        // as a vanilla hand Update would do). The order matters:
        //   - incoming ingested → always refresh snapshot from incoming.
        //   - incoming hand, existing ingested → preserve existing
        //     snapshot so AONT201–AONT208 can still diff hand vs ingested.
        //   - incoming hand, existing hand → no ingested side; clear any
        //     stale snapshot defensively.
        var key = (d.DomainName, d.Name);
        if (d.Source == DescriptorSource.Ingested)
        {
            _ingestedOriginals[key] = d;
        }
        else if (existing.Source == DescriptorSource.Ingested)
        {
            _ingestedOriginals[key] = existing;
        }
        else
        {
            _ingestedOriginals.Remove(key);
        }
    }

    private void ApplyRemoveObjectType(OntologyDelta.RemoveObjectType delta)
    {
        var idx = FindObjectTypeIndex(delta.DomainName, delta.TypeName);
        if (idx >= 0)
        {
            _objectTypes.RemoveAt(idx);
        }

        // DR-7: drop any ingested-original snapshot for this
        // (DomainName, Name) so a subsequent Add for the same key
        // doesn't compare against a stale pre-merge snapshot.
        _ingestedOriginals.Remove((delta.DomainName, delta.TypeName));
    }

    /// <summary>
    /// DR-7: keep <see cref="_ingestedOriginals"/> consistent with the
    /// descriptor at <c>(descriptor.DomainName, descriptor.Name)</c>.
    /// An ingested descriptor refreshes the snapshot; a hand-authored
    /// descriptor (overwriting a previous ingested entry via Update)
    /// drops the snapshot so AONT201–AONT208 don't compare against the
    /// stale ingested side.
    /// </summary>
    private void SyncIngestedOriginal(ObjectTypeDescriptor descriptor)
    {
        var key = (descriptor.DomainName, descriptor.Name);
        if (descriptor.Source == DescriptorSource.Ingested)
        {
            _ingestedOriginals[key] = descriptor;
        }
        else
        {
            _ingestedOriginals.Remove(key);
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

        // DR-7: mirror the same property mutation into the ingested
        // snapshot so DR-7 diagnostics don't read stale pre-delta
        // properties. No-op if there's no ingested baseline for this key.
        MutateIngestedOriginalProperties(
            (delta.DomainName, delta.TypeName),
            props => props.Add(delta.Descriptor));
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

        MutateIngestedOriginalProperties(
            (delta.DomainName, delta.TypeName),
            props =>
            {
                for (var i = 0; i < props.Count; i++)
                {
                    if (props[i].Name == delta.FromName)
                    {
                        props[i] = props[i] with { Name = delta.ToName };
                    }
                }
            });
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

        MutateIngestedOriginalProperties(
            (delta.DomainName, delta.TypeName),
            props => props.RemoveAll(p => p.Name == delta.PropertyName));
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

        MutateIngestedOriginalLinks(
            (delta.DomainName, delta.SourceTypeName),
            links => links.Add(delta.Descriptor));
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

        MutateIngestedOriginalLinks(
            (delta.DomainName, delta.SourceTypeName),
            links => links.RemoveAll(l => l.Name == delta.LinkName));
    }

    /// <summary>
    /// DR-7: apply <paramref name="mutate"/> to the ingested-original
    /// property list at <paramref name="key"/> if one exists; no-op if
    /// the key has no ingested baseline (the snapshot drives DR-7
    /// diagnostics only when both sides contributed).
    /// </summary>
    private void MutateIngestedOriginalProperties(
        (string DomainName, string Name) key,
        Action<List<PropertyDescriptor>> mutate)
    {
        if (!_ingestedOriginals.TryGetValue(key, out var current))
        {
            return;
        }

        var properties = current.Properties.ToList();
        mutate(properties);
        _ingestedOriginals[key] = current with { Properties = properties.AsReadOnly() };
    }

    /// <summary>
    /// DR-7 link-mutation mirror — see
    /// <see cref="MutateIngestedOriginalProperties"/>.
    /// </summary>
    private void MutateIngestedOriginalLinks(
        (string DomainName, string Name) key,
        Action<List<LinkDescriptor>> mutate)
    {
        if (!_ingestedOriginals.TryGetValue(key, out var current))
        {
            return;
        }

        var links = current.Links.ToList();
        mutate(links);
        _ingestedOriginals[key] = current with { Links = links.AsReadOnly() };
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
