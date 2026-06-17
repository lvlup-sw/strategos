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
