namespace Strategos.Ontology.Temporal;

/// <summary>
/// The deterministic terminal state of replaying a bitemporal association event
/// stream (DR-16, T22, #126). Folding the SAME ordered log always yields an
/// equal result — the INV-7 replay-determinism guarantee.
/// </summary>
/// <remarks>
/// INV-6 (sealed) / INV-7 (immutable): a sealed record over an immutable,
/// totally-ordered <see cref="Rows"/> list, so structural equality compares the
/// projected set element-for-element.
/// </remarks>
public sealed record TemporalAssociationProjection
{
    private TemporalAssociationProjection(IReadOnlyList<TemporalRow> rows) => Rows = rows;

    /// <summary>
    /// The projected temporal rows in a STABLE total order (ordinal by association
    /// id, then by <c>SystemFrom</c>) — never raw fold/insertion order — so a
    /// second replay enumerates identically (INV-7).
    /// </summary>
    public IReadOnlyList<TemporalRow> Rows { get; }

    /// <summary>
    /// Replays an append-only association event stream into its deterministic
    /// terminal state. An <see cref="AssociationTemporalEventKind.Assert"/> opens a
    /// row; a <see cref="AssociationTemporalEventKind.Retract"/> CLOSES the matching
    /// open row's system interval (re-deriving the row with a closed
    /// <c>system_to</c>) — it NEVER deletes (INV-7: soft-delete via interval close).
    /// A retract for an unknown / already-closed association folds to a no-op so the
    /// projection stays well-defined on an out-of-order or duplicated stream.
    /// </summary>
    /// <param name="events">The ordered append-only event stream.</param>
    public static TemporalAssociationProjection Replay(IEnumerable<AssociationTemporalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // Keyed by association id so a retract finds its open assertion. A linked
        // dictionary value is the in-progress row; the close event rewrites it
        // (a fresh immutable record) rather than mutating in place.
        var byAssociation = new Dictionary<string, TemporalRow>(StringComparer.Ordinal);

        foreach (var evt in events)
        {
            switch (evt.Kind)
            {
                case AssociationTemporalEventKind.Assert:
                    // Idempotent on a duplicate assert: a re-assert while the row is
                    // still OPEN (SystemTo is null) is a NO-OP that PRESERVES the
                    // original open interval — symmetric with the retract no-op below
                    // and consistent with RelateAsync being idempotent on a duplicate
                    // (src, link, tgt). A re-assert AFTER the prior interval was closed
                    // (SystemTo set) legitimately opens a fresh interval, so the guard
                    // is specifically "exists AND still open".
                    if (byAssociation.TryGetValue(evt.AssociationId, out var existing)
                        && existing.SystemTo is null)
                    {
                        break;
                    }

                    byAssociation[evt.AssociationId] = new TemporalRow
                    {
                        AssociationId = evt.AssociationId,
                        SourceId = evt.SourceId!,
                        TargetId = evt.TargetId!,
                        ValidFrom = evt.ValidFrom!.Value,
                        ValidTo = evt.ValidTo,
                        SystemFrom = evt.OccurredAt,
                        SystemTo = null,
                    };
                    break;

                case AssociationTemporalEventKind.Retract:
                    // Close the matching OPEN row's system interval. Unknown or
                    // already-closed -> no-op (keeps replay well-defined). The close
                    // re-derives the row; no mutation, no delete.
                    if (byAssociation.TryGetValue(evt.AssociationId, out var open)
                        && open.SystemTo is null)
                    {
                        byAssociation[evt.AssociationId] = open with { SystemTo = evt.OccurredAt };
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        $"{nameof(events)}.{nameof(AssociationTemporalEvent.Kind)}",
                        evt.Kind,
                        "Unsupported association temporal event kind.");
            }
        }

        // Impose a STABLE total order on the output so enumeration is deterministic
        // across replays — a Dictionary's enumeration order is unspecified, so the
        // sort is load-bearing for the INV-7 replay guarantee.
        var ordered = byAssociation.Values
            .OrderBy(r => r.AssociationId, StringComparer.Ordinal)
            .ThenBy(r => r.SystemFrom)
            .ToList();

        return new TemporalAssociationProjection(ordered);
    }
}
