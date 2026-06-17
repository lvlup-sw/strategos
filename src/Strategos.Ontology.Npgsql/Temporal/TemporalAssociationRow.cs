namespace Strategos.Ontology.Npgsql.Temporal;

/// <summary>
/// The bitemporal projection of ONE reified-association assertion (DR-16, #126):
/// the two endpoint ids plus the XTDB-style quartet — a user-asserted VALID
/// interval and an infra-derived SYSTEM (transaction-time) interval. It is the
/// typed shape the association event stream projects to, and the row the
/// as-of-transaction-time reconstruction (<see
/// cref="Internal.SqlGenerator.BuildAsOfTransactionTimeSql"/>) reads back.
/// </summary>
/// <remarks>
/// <para>
/// Both intervals are half-open <c>[from, to)</c>. <c>ValidFrom</c> /
/// <c>SystemFrom</c> are always present; <c>ValidTo</c> / <c>SystemTo</c> are
/// <c>null</c> while the interval is OPEN (an unbounded valid-time, or a
/// currently-asserted system-time). <c>SystemTo IS NULL</c> is the as-of-now
/// class: the assertion has not been retracted.
/// </para>
/// <para>
/// INV-7 (immutable / event-derived): a sealed <c>init</c>-only record carrying
/// NO mutation surface. Transaction-time is DERIVED from the append-only
/// association event stream — a retraction APPENDS a close event that sets
/// <see cref="SystemTo"/>; it never physically deletes the row. INV-8 (polyglot
/// identity): endpoints are addressed by their projected string id, never a CLR
/// <see cref="System.Type"/>.
/// </para>
/// </remarks>
public sealed record TemporalAssociationRow
{
    /// <summary>The reified association object's projected business id.</summary>
    public required string AssociationId { get; init; }

    /// <summary>The source endpoint's projected business id.</summary>
    public required string SourceId { get; init; }

    /// <summary>The target endpoint's projected business id.</summary>
    public required string TargetId { get; init; }

    /// <summary>
    /// Inclusive lower bound of the USER-asserted valid interval — when the
    /// relationship is asserted to hold in the modeled world.
    /// </summary>
    public required DateTimeOffset ValidFrom { get; init; }

    /// <summary>
    /// Exclusive upper bound of the valid interval, or <c>null</c> for an
    /// unbounded (still-holding) valid-time.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }

    /// <summary>
    /// Inclusive lower bound of the infra-derived SYSTEM (transaction-time)
    /// interval — when the assertion was recorded in the store. Derived from the
    /// appending event's timestamp, never user-supplied.
    /// </summary>
    public required DateTimeOffset SystemFrom { get; init; }

    /// <summary>
    /// Exclusive upper bound of the system interval, or <c>null</c> while the
    /// assertion is OPEN (not retracted). A retraction CLOSES this to the close
    /// event's timestamp; the row is never deleted (INV-7).
    /// </summary>
    public DateTimeOffset? SystemTo { get; init; }
}
