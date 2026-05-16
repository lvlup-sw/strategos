using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Typed sub-record attached to <see cref="ResponseMeta.Hybrid"/> whenever the
/// hybrid (dense + sparse) retrieval path was engaged. Surfaces both healthy and
/// degraded states to operators via the MCP <c>_meta</c> envelope per design §6.5.
/// </summary>
/// <param name="Hybrid">
/// <c>true</c> only when the sparse leg actually contributed results to fusion.
/// <c>false</c> on degraded paths (<see cref="Degraded"/> populated) and on the
/// <c>EnableKeyword == false</c> explicit-dense-only path.
/// </param>
/// <param name="FusionMethod">
/// Lowercase-snake fusion method tag. Values: <c>"reciprocal"</c> or
/// <c>"distribution_based"</c>. Present only when <paramref name="Hybrid"/> is
/// <c>true</c>; omitted from the wire when null.
/// </param>
/// <param name="Degraded">
/// Degradation reason tag when the hybrid path declined to its dense-only
/// fallback. Values: <c>"no-keyword-provider"</c> or <c>"sparse-failed"</c>.
/// Null on healthy paths; omitted from the wire when null.
/// </param>
/// <param name="DenseTopScore">
/// Highest dense-leg similarity score observed for this query. Null on degraded
/// shapes; omitted from the wire when null.
/// </param>
/// <param name="SparseTopScore">
/// Highest sparse-leg keyword score observed for this query (provider-specific
/// scale; commonly BM25). Null on degraded shapes; omitted from the wire when
/// null.
/// </param>
/// <param name="BmSaturationThreshold">
/// Informational mirror of <c>HybridQueryOptions.BmSaturationThreshold</c>. Null
/// on degraded shapes; omitted from the wire when null. Does not affect fusion
/// math in 2.6.0 — surfaced so operators can correlate observability with the
/// configured threshold.
/// </param>
/// <remarks>
/// All optional fields are serialized with <see cref="JsonIgnoreCondition.WhenWritingNull"/>
/// so the degraded wire shape is the minimal pair <c>{ "hybrid", "degraded" }</c>
/// and the healthy shape carries all five score/method keys.
/// </remarks>
public sealed record HybridMeta(
    [property: JsonPropertyName("hybrid")] bool Hybrid,
    [property: JsonPropertyName("fusionMethod")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FusionMethod = null,
    [property: JsonPropertyName("degraded")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Degraded = null,
    [property: JsonPropertyName("denseTopScore")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? DenseTopScore = null,
    [property: JsonPropertyName("sparseTopScore")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? SparseTopScore = null,
    [property: JsonPropertyName("bmSaturationThreshold")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? BmSaturationThreshold = null);
