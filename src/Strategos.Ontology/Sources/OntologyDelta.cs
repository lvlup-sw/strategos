namespace Strategos.Ontology;

/// <summary>
/// Abstract base of the ontology event vocabulary emitted by
/// <see cref="IOntologySource"/> implementations.
/// </summary>
/// <remarks>
/// DR-4 (Task 8) supplies the eight concrete variants. This file
/// reserves the sealed-abstract shell — only <c>SourceId</c> and
/// <c>Timestamp</c> required-init fields — so that the
/// <see cref="IOntologySource"/> interface (Task 7) can refer to the
/// base type without yet depending on the variant catalog.
/// </remarks>
public abstract record OntologyDelta
{
    /// <summary>Identifier of the <see cref="IOntologySource"/> emitting the delta.</summary>
    public required string SourceId { get; init; }

    /// <summary>Wall-clock instant of the originating change.</summary>
    public required DateTimeOffset Timestamp { get; init; }
}
