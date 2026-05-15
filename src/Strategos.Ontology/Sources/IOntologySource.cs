namespace Strategos.Ontology;

/// <summary>
/// Extension point enabling ontology graph contributions from sources
/// beyond hand-authored <c>DomainOntology.Define()</c>. Registered via DI
/// (see <c>OntologyOptions.AddSource&lt;T&gt;()</c>). Drained at startup
/// by <see cref="OntologyGraphBuilder"/>.
/// </summary>
/// <remarks>
/// DR-3 (Task 7). The Strategos 2.5.0 consumer ships the <see cref="LoadAsync"/>
/// drain; <see cref="SubscribeAsync"/> is part of the surface contract for
/// forward compatibility with live invalidation (v2.6.0+), and may
/// complete immediately for static sources.
/// </remarks>
public interface IOntologySource
{
    /// <summary>
    /// Stable identifier; tags provenance and conflict diagnostics.
    /// Used as the <c>SourceId</c> on emitted <see cref="OntologyDelta"/>
    /// values and threaded through any composition exception originating
    /// from this source.
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// Replays the source's full state as a stream of deltas. Called once
    /// at <see cref="OntologyGraphBuilder.Build"/> time.
    /// </summary>
    IAsyncEnumerable<OntologyDelta> LoadAsync(CancellationToken ct);

    /// <summary>
    /// Subscribes to incremental updates. Empty for static sources.
    /// </summary>
    IAsyncEnumerable<OntologyDelta> SubscribeAsync(CancellationToken ct);
}
