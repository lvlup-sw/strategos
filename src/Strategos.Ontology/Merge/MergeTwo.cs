using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Merge;

/// <summary>
/// Two-input lattice fold over <see cref="ObjectTypeDescriptor"/>, combining
/// a hand-authored contribution with an ingested contribution per the
/// lattice rule from basileus ADR §9.1–§9.2.
/// </summary>
/// <remarks>
/// <para>
/// Identity-field rule (DR-6 lateral):
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ObjectTypeDescriptor.ClrType"/>: hand wins, fallback to ingested.</description></item>
/// <item><description><see cref="ObjectTypeDescriptor.SymbolKey"/>: ingested wins (SCIP authoritative), fallback to hand.</description></item>
/// <item><description><see cref="ObjectTypeDescriptor.SymbolFqn"/>: ingested wins, fallback to hand.</description></item>
/// <item><description><see cref="ObjectTypeDescriptor.LanguageId"/>: hand wins.</description></item>
/// <item><description><see cref="ObjectTypeDescriptor.Source"/>: always <see cref="DescriptorSource.HandAuthored"/> — hand wins on composition.</description></item>
/// <item><description><see cref="ObjectTypeDescriptor.Name"/> and <see cref="ObjectTypeDescriptor.DomainName"/>: taken from hand; mismatch with ingested surfaces as AONT006 upstream.</description></item>
/// </list>
/// <para>
/// Intent-only fields are always taken from hand:
/// <see cref="ObjectTypeDescriptor.Actions"/>,
/// <see cref="ObjectTypeDescriptor.Events"/>,
/// <see cref="ObjectTypeDescriptor.Lifecycle"/>. A mechanical ingester
/// contributing to any of these is reported as AONT205 upstream.
/// </para>
/// <para>
/// Per-name union for <see cref="ObjectTypeDescriptor.Properties"/> and
/// <see cref="ObjectTypeDescriptor.Links"/> is implemented in Task 15.
/// </para>
/// </remarks>
public static class MergeTwo
{
    /// <summary>
    /// Merges a hand-authored descriptor with an ingested descriptor
    /// per the DR-6 lateral lattice rule.
    /// </summary>
    /// <param name="hand">Hand-authored contribution.</param>
    /// <param name="ingested">Ingested contribution.</param>
    /// <returns>
    /// A new <see cref="ObjectTypeDescriptor"/> with merged identity
    /// fields and hand-only intent fields.
    /// </returns>
    public static ObjectTypeDescriptor Merge(
        ObjectTypeDescriptor hand,
        ObjectTypeDescriptor ingested)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(ingested);

        return new ObjectTypeDescriptor
        {
            Name = hand.Name,
            DomainName = hand.DomainName,
            ClrType = hand.ClrType ?? ingested.ClrType,
            SymbolKey = ingested.SymbolKey ?? hand.SymbolKey,
            SymbolFqn = ingested.SymbolFqn ?? hand.SymbolFqn,
            LanguageId = hand.LanguageId,
            Source = DescriptorSource.HandAuthored,
            SourceId = hand.SourceId,
            IngestedAt = hand.IngestedAt,
            KeyProperty = hand.KeyProperty,
            Properties = MergeProperties(hand.Properties, ingested.Properties),
            Links = MergeLinks(hand.Links, ingested.Links),
            Actions = hand.Actions,
            Events = hand.Events,
            ImplementedInterfaces = hand.ImplementedInterfaces,
            Lifecycle = hand.Lifecycle,
            InterfaceActionMappings = hand.InterfaceActionMappings,
            ExternalLinkExtensionPoints = hand.ExternalLinkExtensionPoints,
            InterfacePropertyMappings = hand.InterfacePropertyMappings,
            Kind = hand.Kind,
            ParentType = hand.ParentType,
            ParentTypeName = hand.ParentTypeName,
        };
    }

    /// <summary>
    /// Per-name union of <see cref="PropertyDescriptor"/> collections.
    /// Hand wins on conflict; ingested-only entries are restamped with
    /// <see cref="DescriptorSource.Ingested"/> so downstream consumers
    /// can distinguish origin.
    /// </summary>
    /// <param name="hand">Hand-authored properties.</param>
    /// <param name="ingested">Ingested properties.</param>
    /// <returns>Merged property list (deterministic order: hand first, then ingested-only).</returns>
    public static IReadOnlyList<PropertyDescriptor> MergeProperties(
        IReadOnlyList<PropertyDescriptor> hand,
        IReadOnlyList<PropertyDescriptor> ingested)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(ingested);

        // Hand entries pass through untouched (preserving their original Source).
        var result = new List<PropertyDescriptor>(hand.Count + ingested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in hand)
        {
            result.Add(p);
            seen.Add(p.Name);
        }

        // Ingested-only entries are appended, restamped with Source = Ingested.
        // Mark newly-appended names in `seen` so a duplicate name within
        // `ingested` itself collapses to a single emission (rule: ingested
        // duplicates are silently de-duped — they're mechanical noise, not
        // intent — same as the hand-side de-dup behavior).
        foreach (var p in ingested)
        {
            if (!seen.Add(p.Name))
            {
                continue;
            }

            result.Add(p with { Source = DescriptorSource.Ingested });
        }

        // AsReadOnly wraps the backing list so consumers cannot downcast
        // to List<T> and mutate the merged collection.
        return result.AsReadOnly();
    }

    /// <summary>
    /// Per-name union of <see cref="LinkDescriptor"/> collections.
    /// Hand wins on conflict; ingested-only entries are restamped with
    /// <see cref="DescriptorSource.Ingested"/>.
    /// </summary>
    /// <param name="hand">Hand-authored links.</param>
    /// <param name="ingested">Ingested links.</param>
    /// <returns>Merged link list (deterministic order: hand first, then ingested-only).</returns>
    public static IReadOnlyList<LinkDescriptor> MergeLinks(
        IReadOnlyList<LinkDescriptor> hand,
        IReadOnlyList<LinkDescriptor> ingested)
    {
        ArgumentNullException.ThrowIfNull(hand);
        ArgumentNullException.ThrowIfNull(ingested);

        var result = new List<LinkDescriptor>(hand.Count + ingested.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var l in hand)
        {
            result.Add(l);
            seen.Add(l.Name);
        }

        foreach (var l in ingested)
        {
            if (!seen.Add(l.Name))
            {
                continue;
            }

            result.Add(l with { Source = DescriptorSource.Ingested });
        }

        // AsReadOnly wraps the backing list so consumers cannot downcast
        // to List<T> and mutate the merged collection.
        return result.AsReadOnly();
    }
}
