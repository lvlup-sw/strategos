namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// An immutable materialized relation row: the target endpoint of a stored
/// link instance, plus a reserved slot for a DR-4 attributed-relate
/// association object.
/// </summary>
/// <remarks>
/// INV-7 (replay determinism): the row is an immutable record, and the
/// relate-store's read path returns rows in a deterministic, ordinal-by-id
/// order — never raw insertion order or <see cref="System.Collections.Concurrent"/>
/// enumeration order.
/// <para>
/// <see cref="AssociationObjectId"/> is RESERVED for DR-4 (attributed
/// relate). DR-2 always leaves it null; DR-4 will populate it without
/// reshaping the row.
/// </para>
/// </remarks>
/// <param name="TargetDescriptor">The descriptor name of the target endpoint.</param>
/// <param name="TargetId">The projected id of the target instance.</param>
/// <param name="AssociationObjectId">
/// Reserved for DR-4. The projected id of the association object backing an
/// attributed relate; null for a plain (unattributed) DR-2 relation.
/// </param>
public sealed record RelationRow(
    string TargetDescriptor,
    string TargetId,
    string? AssociationObjectId = null);
