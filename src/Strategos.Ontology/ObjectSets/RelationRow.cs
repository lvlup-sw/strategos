namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// An immutable materialized relation row: the target endpoint of a stored
/// link instance, plus the id of the DR-4 attributed-relate association object
/// backing the row (null for a plain DR-2 relation).
/// </summary>
/// <remarks>
/// INV-7 (replay determinism): the row is an immutable record, and the
/// relate-store's read path returns rows in a deterministic, ordinal-by-id
/// order — never raw insertion order or <see cref="System.Collections.Concurrent"/>
/// enumeration order.
/// <para>
/// <see cref="AssociationObjectId"/> backs the DR-4 attributed relate. A plain
/// DR-2 relate leaves it null; an attributed relate populates it with the
/// association object's projected id, without reshaping the row.
/// </para>
/// </remarks>
/// <param name="TargetDescriptor">The descriptor name of the target endpoint.</param>
/// <param name="TargetId">The projected id of the target instance.</param>
/// <param name="AssociationObjectId">
/// The projected id of the association object backing a DR-4 attributed relate;
/// null for a plain (unattributed) DR-2 relation.
/// </param>
public sealed record RelationRow(
    string TargetDescriptor,
    string TargetId,
    string? AssociationObjectId = null);
