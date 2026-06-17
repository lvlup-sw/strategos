namespace Strategos.Ontology.ObjectSets;

/// <summary>
/// One pure-link relate operation in a <see cref="IObjectSetWriter.RelateBatchAsync"/>
/// batch (DR-13/R6): the same five operands the single-pair
/// <see cref="IObjectSetWriter.RelateAsync(string, string, string, string, string, System.Threading.CancellationToken)"/>
/// takes, bundled so bulk edge ingestion (#115) can submit many relations in one
/// call without a round-trip per edge.
/// </summary>
/// <remarks>
/// INV-6/INV-7: a sealed, <c>init</c>-only immutable record — a batch request is
/// inert data, never mutated after construction. This reserves only the PLAIN
/// (unattributed) relate shape; the attributed (association-object) batch is out
/// of scope for the R6 reservation and remains single-pair via
/// <see cref="IObjectSetWriter.RelateAsync{TRel}"/>.
/// </remarks>
public sealed record RelateRequest
{
    /// <summary>Descriptor name of the source endpoint.</summary>
    public required string SourceDescriptor { get; init; }

    /// <summary>Projected id of the source instance.</summary>
    public required string SourceId { get; init; }

    /// <summary>Name of the link being materialized.</summary>
    public required string LinkName { get; init; }

    /// <summary>Descriptor name of the target endpoint.</summary>
    public required string TargetDescriptor { get; init; }

    /// <summary>Projected id of the target instance.</summary>
    public required string TargetId { get; init; }
}
