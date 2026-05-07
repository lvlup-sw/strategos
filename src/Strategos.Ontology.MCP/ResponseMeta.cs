namespace Strategos.Ontology.MCP;

/// <summary>
/// Per-response metadata threaded through every ontology MCP tool result.
/// Consumers use <see cref="OntologyVersion"/> to invalidate schema caches when
/// the ontology graph mutates (Strategos 2.6.0 hybrid retrieval cache;
/// Exarchos client-side schema cache).
/// </summary>
/// <param name="OntologyVersion">
/// Wire-format version identifier — hash digest prefixed with the algorithm
/// (e.g. <c>"sha256:..."</c>). Constructed via <see cref="ForGraph"/> so the
/// prefix is applied uniformly at the meta-envelope boundary; the underlying
/// <c>OntologyGraph.Version</c> property returns bare hex.
/// </param>
public sealed record ResponseMeta(string OntologyVersion)
{
    /// <summary>
    /// Builds a <see cref="ResponseMeta"/> from the supplied graph, stamping
    /// the wire-format prefix onto the bare hex version.
    /// </summary>
    public static ResponseMeta ForGraph(OntologyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new ResponseMeta(WireFormat(graph.Version));
    }

    /// <summary>
    /// Idempotently prepends the <c>"sha256:"</c> wire-format prefix.
    /// Already-prefixed values are returned unchanged so this can be applied
    /// to graph version strings of either shape without doubling.
    /// </summary>
    internal static string WireFormat(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.StartsWith("sha256:", StringComparison.Ordinal)
            ? version
            : "sha256:" + version;
    }
}
