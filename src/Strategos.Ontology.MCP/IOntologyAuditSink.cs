namespace Strategos.Ontology.MCP;

/// <summary>
/// The audit seam for recorded abstentions (DR-17, emission half). Consumed
/// EXCLUSIVELY by <see cref="OntologyAnswerComposer"/>: whenever the composer produces
/// a <see cref="NoAnswerRecorded"/>, it emits an <see cref="OntologyAbstainedRecord"/>
/// through this sink. Because the answer union's leaf constructors are internal and the
/// composer is the sole producer (DR-11), bypassing the composer cannot construct the
/// union at all — so every abstention necessarily flows through this sink.
/// </summary>
/// <remarks>
/// The abstraction adds NO new package dependency to <c>Strategos.Ontology.MCP</c>: the
/// default is the no-op <see cref="NoOpOntologyAuditSink"/>, and a host wires a concrete
/// sink (e.g. a logging one) via its hosting bridge. The default keeps existing consumers
/// source- and behavior-compatible.
/// </remarks>
public interface IOntologyAuditSink
{
    /// <summary>
    /// Records one abstention. Called synchronously by the composer at the point it
    /// produces a <see cref="NoAnswerRecorded"/>, before that value is returned.
    /// </summary>
    /// <param name="record">The abstention audit record (carries the nearest-records COUNT, never contents).</param>
    void RecordAbstention(OntologyAbstainedRecord record);
}
