using System.Text.Json.Serialization;

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
/// <c>OntologyGraph.Version</c> property returns bare hex. The wire-format key
/// is pinned to <c>"ontologyVersion"</c> so the contract is stable regardless
/// of the surrounding <see cref="System.Text.Json.JsonSerializerOptions"/>
/// (the MCP SDK applies <c>JsonSerializerDefaults.Web</c> by default).
/// </param>
public sealed record ResponseMeta(
    [property: JsonPropertyName("ontologyVersion")] string OntologyVersion)
{
    /// <summary>
    /// Hybrid retrieval metadata. <c>null</c> when <c>hybridOptions</c> was not
    /// supplied to <c>OntologyQueryTool.QueryAsync</c>, or when the structural
    /// (non-semantic) branch was taken. Serialized as absent so 2.5.0 snapshots
    /// remain byte-for-byte stable (design §6.5).
    /// </summary>
    [JsonPropertyName("hybrid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HybridMeta? Hybrid { get; init; }

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
