namespace Strategos.Ontology.Temporal;

/// <summary>
/// The kind of a bitemporal association event in the append-only stream
/// (DR-16, T22, #126).
/// </summary>
public enum AssociationTemporalEventKind
{
    /// <summary>An assertion that a relationship holds — opens a system interval.</summary>
    Assert,

    /// <summary>A retraction of a prior assertion — CLOSES its system interval (no delete).</summary>
    Retract,
}

/// <summary>
/// One event in the append-only bitemporal association stream (DR-16, T22, #126).
/// Transaction-time is DERIVED from this stream: an <see cref="AssociationTemporalEventKind.Assert"/>
/// opens a system interval at <see cref="SystemFrom"/>; a
/// <see cref="AssociationTemporalEventKind.Retract"/> CLOSES the matching
/// assertion's interval at <see cref="SystemTo"/>. A retraction NEVER deletes —
/// it appends a close event, which the <see cref="TemporalAssociationProjection"/>
/// folds into a closed <c>system_to</c> on the row (INV-7: the soft-delete axis).
/// </summary>
/// <remarks>
/// <para>
/// INV-6 (sealed) / INV-7 (immutable, event-derived): a sealed <c>init</c>-only
/// record with NO mutation surface. The event carries its OWN timestamps so the
/// projection reads transaction-time off the stream rather than a fold-time wall
/// clock — that is what keeps replay deterministic. Construct via
/// <see cref="Assert"/> / <see cref="Retract"/> so the per-kind field obligations
/// are enforced at the factory.
/// </para>
/// <para>
/// <see cref="AssertingAgent"/> is the opaque, header-safe value of the agent
/// attributed to the event — the G1 <c>CurrentAgentIdentity</c> seam carried as a
/// string (mirrors <c>AgentIdentity.Value</c>) so the self-contained core
/// ontology (INV-2) does not take a dependency on the identity package. The
/// binding to <c>IAgentIdentityAccessor.CurrentAgent</c> happens at the call site
/// that constructs the event.
/// </para>
/// </remarks>
public sealed record AssociationTemporalEvent
{
    /// <summary>The kind of event (assert / retract).</summary>
    public required AssociationTemporalEventKind Kind { get; init; }

    /// <summary>The reified association object's projected business id.</summary>
    public required string AssociationId { get; init; }

    /// <summary>The source endpoint's projected business id (assert only; null on retract).</summary>
    public string? SourceId { get; init; }

    /// <summary>The target endpoint's projected business id (assert only; null on retract).</summary>
    public string? TargetId { get; init; }

    /// <summary>Inclusive lower bound of the user-asserted valid interval (assert only).</summary>
    public DateTimeOffset? ValidFrom { get; init; }

    /// <summary>Exclusive upper bound of the valid interval, or null for unbounded (assert only).</summary>
    public DateTimeOffset? ValidTo { get; init; }

    /// <summary>
    /// The infra-derived transaction instant of this event: the system-interval
    /// OPEN instant on an assert, the system-interval CLOSE instant on a retract.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// The opaque, header-safe value of the agent attributed to this event
    /// (the <c>CurrentAgentIdentity</c> seam carried as a string), or <c>null</c>
    /// when no agent context was active.
    /// </summary>
    public string? AssertingAgent { get; init; }

    /// <summary>
    /// Creates an <see cref="AssociationTemporalEventKind.Assert"/> event opening a
    /// system interval at <paramref name="systemFrom"/>.
    /// </summary>
    /// <param name="associationId">The association object's projected business id.</param>
    /// <param name="sourceId">The source endpoint's projected business id.</param>
    /// <param name="targetId">The target endpoint's projected business id.</param>
    /// <param name="validFrom">Inclusive lower bound of the user-asserted valid interval.</param>
    /// <param name="validTo">Exclusive upper bound of the valid interval, or null.</param>
    /// <param name="systemFrom">The infra-derived instant the assertion was recorded.</param>
    /// <param name="assertingAgent">The asserting agent's header-safe value, or null.</param>
    public static AssociationTemporalEvent Assert(
        string associationId,
        string sourceId,
        string targetId,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        DateTimeOffset systemFrom,
        string? assertingAgent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(associationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        return new AssociationTemporalEvent
        {
            Kind = AssociationTemporalEventKind.Assert,
            AssociationId = associationId,
            SourceId = sourceId,
            TargetId = targetId,
            ValidFrom = validFrom,
            ValidTo = validTo,
            OccurredAt = systemFrom,
            AssertingAgent = assertingAgent,
        };
    }

    /// <summary>
    /// Creates an <see cref="AssociationTemporalEventKind.Retract"/> event CLOSING
    /// the matching assertion's system interval at <paramref name="systemTo"/>.
    /// </summary>
    /// <param name="associationId">The association object id whose interval to close.</param>
    /// <param name="systemTo">The infra-derived instant the assertion was retracted.</param>
    /// <param name="retractingAgent">The retracting agent's header-safe value, or null.</param>
    public static AssociationTemporalEvent Retract(
        string associationId,
        DateTimeOffset systemTo,
        string? retractingAgent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(associationId);

        return new AssociationTemporalEvent
        {
            Kind = AssociationTemporalEventKind.Retract,
            AssociationId = associationId,
            OccurredAt = systemTo,
            AssertingAgent = retractingAgent,
        };
    }
}
