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
            Properties = hand.Properties,
            Links = hand.Links,
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
}
