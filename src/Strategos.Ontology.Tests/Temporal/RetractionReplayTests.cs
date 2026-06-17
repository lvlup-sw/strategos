using Strategos.Ontology.Temporal;

namespace Strategos.Ontology.Tests.Temporal;

/// <summary>
/// DR-16 (T22, #126): the INV-7 replay-determinism guard for the bitemporal
/// association projection. A retraction is NOT a physical delete — it APPENDS a
/// CLOSE event to the append-only stream, and the projection folds that into a
/// closed <c>system_to</c> on the matching row. The keystone property: folding
/// the SAME ordered event log twice yields IDENTICAL terminal state (no hidden
/// nondeterminism, no order-dependent collapse, no row removal).
/// </summary>
/// <remarks>
/// Pure in-memory replay — NO database (the live persistence of the same model is
/// the Npgsql T20/T21 projection). Transaction-time is DERIVED from the event
/// stream: each event carries the appending instant, and the projection reads it
/// back, never <c>DateTimeOffset.UtcNow</c> at fold time (a wall-clock read would
/// break replay determinism).
/// </remarks>
public class RetractionReplayTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    private static readonly DateTimeOffset T1 = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
    private static readonly DateTimeOffset T2 = DateTimeOffset.Parse("2026-06-01T00:00:00Z");

    private static IReadOnlyList<AssociationTemporalEvent> SampleLog() =>
    [
        AssociationTemporalEvent.Assert(
            associationId: "emp-1",
            sourceId: "p1",
            targetId: "c1",
            validFrom: T0,
            validTo: null,
            systemFrom: T0,
            assertingAgent: "agent-a"),
        AssociationTemporalEvent.Assert(
            associationId: "emp-2",
            sourceId: "p2",
            targetId: "c1",
            validFrom: T0,
            validTo: null,
            systemFrom: T1,
            assertingAgent: "agent-b"),
        // Retract the first assertion at T2 — a CLOSE event, never a delete.
        AssociationTemporalEvent.Retract(
            associationId: "emp-1",
            systemTo: T2,
            retractingAgent: "agent-a"),
    ];

    [Test]
    public async Task Retract_ClosesTransactionInterval_NoPhysicalDelete_ReplayDeterministic()
    {
        var log = SampleLog();

        // Fold the SAME ordered log twice through independent projections.
        var first = TemporalAssociationProjection.Replay(log);
        var second = TemporalAssociationProjection.Replay(log);

        // INV-7 keystone: two replays of the same event log produce IDENTICAL
        // terminal state. Sealed records give structural equality, so the whole
        // projected set compares equal element-by-element (deterministic order).
        await Assert.That(first.Rows).IsEquivalentTo(second.Rows);
        await Assert.That(first.Rows.SequenceEqual(second.Rows)).IsTrue();

        // The retracted association is NOT deleted — its row survives with a CLOSED
        // system interval (soft-delete via interval close).
        var retracted = first.Rows.Single(r => r.AssociationId == "emp-1");
        await Assert.That(retracted.SystemTo).IsEqualTo(T2);
        await Assert.That(retracted.SystemFrom).IsEqualTo(T0);

        // The non-retracted association stays OPEN (system_to null).
        var open = first.Rows.Single(r => r.AssociationId == "emp-2");
        await Assert.That(open.SystemTo).IsNull();

        // No physical delete: both assertions are still present.
        await Assert.That(first.Rows.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Replay_IsDeterministic_RegardlessOfFoldRepetition()
    {
        // Folding three times must still match the first — the projection holds no
        // mutable cross-fold state, reads no wall clock, and imposes a total order
        // on its output (so a HashSet-style nondeterministic enumeration can never
        // leak in).
        var log = SampleLog();
        var a = TemporalAssociationProjection.Replay(log).Rows;
        var b = TemporalAssociationProjection.Replay(log).Rows;
        var c = TemporalAssociationProjection.Replay(log).Rows;

        await Assert.That(a.SequenceEqual(b)).IsTrue();
        await Assert.That(b.SequenceEqual(c)).IsTrue();
    }

    [Test]
    public async Task Retract_UnknownAssociation_IsIgnored_NotAnError()
    {
        // A close event for an association that was never asserted (out-of-order /
        // duplicate stream) folds to a no-op — the projection stays well-defined
        // and replay-deterministic rather than throwing mid-fold.
        IReadOnlyList<AssociationTemporalEvent> log =
        [
            AssociationTemporalEvent.Retract("ghost", systemTo: T2, retractingAgent: "agent-a"),
        ];

        var projection = TemporalAssociationProjection.Replay(log);

        await Assert.That(projection.Rows).IsEmpty();
    }

    [Test]
    public async Task Replay_OpenAssertion_HasNullSystemTo()
    {
        // An assertion with no matching retraction projects to an OPEN row — the
        // as-of-now class (system_to IS NULL), the dominant query target.
        IReadOnlyList<AssociationTemporalEvent> log =
        [
            AssociationTemporalEvent.Assert("emp-1", "p1", "c1", T0, null, T0, "agent-a"),
        ];

        var row = TemporalAssociationProjection.Replay(log).Rows.Single();

        await Assert.That(row.SystemTo).IsNull();
        await Assert.That(row.ValidFrom).IsEqualTo(T0);
    }
}
