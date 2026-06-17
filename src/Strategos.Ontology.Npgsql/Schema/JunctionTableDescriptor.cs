namespace Strategos.Ontology.Npgsql.Schema;

/// <summary>
/// The resolved per-<c>(link, target-descriptor)</c> identity of one junction
/// table (DR-11, Posture 2, #128). A link is lowered to ONE junction table PER
/// resolved target descriptor; each carries a single HONEST foreign key to that
/// descriptor's object table.
/// </summary>
/// <remarks>
/// <see cref="TargetDescriptorName"/> is the DR-10 graph resolution of the hop's
/// target (override → link <c>TargetTypeName</c> → link <c>TargetSymbolKey</c>),
/// NEVER <c>typeof(TLinked)</c> — so a <c>SymbolKey</c>-only (ingested,
/// <c>ClrType</c> == null) descriptor still yields a junction table. New sealed,
/// <c>init</c>-only record (INV-6/INV-7). Fed unchanged into
/// <see cref="Internal.SqlGenerator.JunctionTableNameFor(JunctionTableDescriptor)"/>
/// and the resolved-targets DDL builder, so the table name the DDL creates and
/// the relate/traverse DML target can never drift.
/// </remarks>
public sealed record JunctionTableDescriptor
{
    /// <summary>The source endpoint's object table name (already snake_cased).</summary>
    public required string SourceTable { get; init; }

    /// <summary>
    /// The link's descriptor name (e.g. <c>"WrittenBy"</c>); snake_cased when the
    /// junction identifier is derived.
    /// </summary>
    public required string LinkName { get; init; }

    /// <summary>
    /// The DR-10 graph-resolved TARGET descriptor name. Identity-bearing: the
    /// junction table name and its FK target derive from this, never from a CLR
    /// type (INV-8).
    /// </summary>
    public required string TargetDescriptorName { get; init; }

    /// <summary>The resolved target descriptor's object table name (snake_cased).</summary>
    public required string TargetTable { get; init; }

    /// <summary>
    /// Whether the link resolves to MORE THAN ONE target descriptor (interface
    /// narrow / multi-registration fan-out). When <c>false</c> (the common
    /// monomorphic case) the junction is named by <c>(source, link)</c> for DML
    /// lockstep with the pre-DR-11 lowering; when <c>true</c> each fanned-out
    /// table is additionally disambiguated by <see cref="TargetDescriptorName"/>
    /// so the per-descriptor tables never collide.
    /// </summary>
    public required bool IsPolymorphic { get; init; }
}
