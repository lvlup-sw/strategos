namespace Strategos.Ontology.MCP;

/// <summary>
/// The audit record emitted whenever the ontology answering surface abstains — the
/// occurrence-side sibling (DR-17) of the <see cref="NoAnswerRecorded"/> arm of the
/// answer union (DR-16). Constructed and emitted ONLY by
/// <see cref="OntologyAnswerComposer"/> through an <see cref="IOntologyAuditSink"/>
/// at the moment it records an abstention.
/// </summary>
/// <remarks>
/// This is a hand-authored twin of the Contracts <c>ontology.abstained</c> wire shape
/// (<c>type</c> + <c>nearestRecordsCount</c>); the ontology layer does NOT reference the
/// Contracts package, so the shape is mirrored by counts, not by a shared type.
/// The payload carries only the COUNT of nearest records — never their identities or
/// contents — so a record identity cannot be exfiltrated through the audit stream.
/// </remarks>
public sealed record OntologyAbstainedRecord
{
    /// <summary>
    /// The envelope type discriminator, pinned to <c>ontology.abstained</c> so the
    /// record maps onto the Contracts <c>OntologyAbstained</c> event shape (DR-17)
    /// without the ontology layer taking a Contracts dependency.
    /// </summary>
    public const string EventType = "ontology.abstained";

    /// <summary>
    /// Constructs the abstention audit record from the COUNT of nearest non-matching
    /// records the abstention surfaced. The count is all that crosses into audit — the
    /// <see cref="RecordRef"/> monikers themselves are never carried here.
    /// </summary>
    /// <param name="nearestRecordsCount">How many nearest records the abstention surfaced; must be non-negative.</param>
    public OntologyAbstainedRecord(int nearestRecordsCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nearestRecordsCount);
        NearestRecordsCount = nearestRecordsCount;
    }

    /// <summary>Envelope type discriminator; always <see cref="EventType"/>.</summary>
    public string Type => EventType;

    /// <summary>
    /// How many nearest non-matching records the abstention surfaced — a COUNT, never
    /// the record identities or contents (no data exfiltration through audit).
    /// </summary>
    public int NearestRecordsCount { get; }
}
