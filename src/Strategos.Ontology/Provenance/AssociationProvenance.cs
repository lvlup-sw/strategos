namespace Strategos.Ontology.Provenance;

/// <summary>
/// The W3C PROV-DM core provenance attached to ONE reified association (DR-16,
/// T23, #126). The association is the PROV <see cref="Entity"/> (the
/// qualified-influence node); it <c>wasGeneratedBy</c> an <see cref="Activity"/>
/// and, when an agent context is active, <c>wasAttributedTo</c> an
/// <see cref="Agent"/> sourced from the G1 <c>CurrentAgentIdentity</c> seam. The
/// <see cref="Influences"/> are the PROV-DM core relations radiating from the
/// node.
/// </summary>
/// <remarks>
/// INV-6 (sealed) / INV-7 (immutable): a sealed record with no mutation surface.
/// Equality is value-based across all members INCLUDING the influence sequence
/// (the compiler-synthesized record equality would compare <see cref="Influences"/>
/// by reference, so equality is overridden to compare it element-wise) — two
/// attachments with identical inputs are structurally equal. INV-8: every node is
/// addressed by id, never a CLR <see cref="System.Type"/>.
/// </remarks>
public sealed record AssociationProvenance
{
    /// <summary>The reified association, modeled as the PROV Entity.</summary>
    public required ProvEntity Entity { get; init; }

    /// <summary>The activity that generated the association assertion.</summary>
    public required ProvActivity Activity { get; init; }

    /// <summary>
    /// The agent the assertion was attributed to (from the
    /// <c>CurrentAgentIdentity</c> seam), or <c>null</c> when no agent context was
    /// active — never fabricated.
    /// </summary>
    public ProvAgent? Agent { get; init; }

    /// <summary>The human-readable reason the assertion was made.</summary>
    public required string Reason { get; init; }

    /// <summary>
    /// The PROV-DM core qualified-influence edges radiating from the node:
    /// always <see cref="ProvRelation.WasGeneratedBy"/> (entity → activity) and
    /// <see cref="ProvRelation.WasAssociatedWith"/> (activity → agent) when an
    /// agent is present, plus <see cref="ProvRelation.WasAttributedTo"/>
    /// (entity → agent) when an agent is present.
    /// </summary>
    public required IReadOnlyList<ProvInfluence> Influences { get; init; }

    /// <inheritdoc />
    public bool Equals(AssociationProvenance? other) =>
        other is not null
        && Entity == other.Entity
        && Activity == other.Activity
        && Agent == other.Agent
        && string.Equals(Reason, other.Reason, StringComparison.Ordinal)
        && Influences.SequenceEqual(other.Influences);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Entity);
        hash.Add(Activity);
        hash.Add(Agent);
        hash.Add(Reason, StringComparer.Ordinal);
        foreach (var influence in Influences)
        {
            hash.Add(influence);
        }

        return hash.ToHashCode();
    }
}
