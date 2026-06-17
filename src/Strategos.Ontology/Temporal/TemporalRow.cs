namespace Strategos.Ontology.Temporal;

/// <summary>
/// The in-memory bitemporal projection of ONE reified-association assertion
/// (DR-16, T22, #126): the association + endpoint ids plus the XTDB-style quartet
/// — a user-asserted VALID interval and an infra-derived SYSTEM (transaction-time)
/// interval. It is the terminal-state row a
/// <see cref="TemporalAssociationProjection"/> folds the append-only event stream
/// into. The Npgsql persistence projection (<c>TemporalAssociationRow</c>) is the
/// same concept materialized to a row; this is the provider-agnostic core shape.
/// </summary>
/// <remarks>
/// <para>
/// Both intervals are half-open <c>[from, to)</c>. <see cref="ValidFrom"/> /
/// <see cref="SystemFrom"/> are always present; <see cref="ValidTo"/> /
/// <see cref="SystemTo"/> are <c>null</c> while the interval is OPEN.
/// <c>SystemTo IS NULL</c> is the as-of-now class: the assertion has not been
/// retracted.
/// </para>
/// <para>
/// INV-7 (immutable / replay-deterministic): a sealed <c>init</c>-only record
/// carrying NO mutation surface. A retraction does NOT mutate or delete the row —
/// the projection re-derives a row whose <see cref="SystemTo"/> is closed to the
/// retraction event's timestamp. Structural record equality is what lets two
/// replays of the same log compare element-for-element equal. INV-8 (polyglot
/// identity): endpoints are addressed by their projected string id, never a CLR
/// <see cref="System.Type"/>.
/// </para>
/// </remarks>
public sealed record TemporalRow
{
    /// <summary>The reified association object's projected business id.</summary>
    public required string AssociationId { get; init; }

    /// <summary>The source endpoint's projected business id.</summary>
    public required string SourceId { get; init; }

    /// <summary>The target endpoint's projected business id.</summary>
    public required string TargetId { get; init; }

    /// <summary>Inclusive lower bound of the user-asserted valid interval.</summary>
    public required DateTimeOffset ValidFrom { get; init; }

    /// <summary>
    /// Exclusive upper bound of the valid interval, or <c>null</c> for an
    /// unbounded (still-holding) valid-time.
    /// </summary>
    public DateTimeOffset? ValidTo { get; init; }

    /// <summary>
    /// Inclusive lower bound of the infra-derived SYSTEM interval — the appending
    /// event's timestamp, never a fold-time wall-clock read.
    /// </summary>
    public required DateTimeOffset SystemFrom { get; init; }

    /// <summary>
    /// Exclusive upper bound of the system interval, or <c>null</c> while the
    /// assertion is OPEN (not retracted). A retraction CLOSES this to the close
    /// event's timestamp; the row is never deleted (INV-7).
    /// </summary>
    public DateTimeOffset? SystemTo { get; init; }
}
