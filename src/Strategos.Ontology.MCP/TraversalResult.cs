using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// One far-endpoint reached by an instance-anchored traversal (DR-15): the
/// destination endpoint instance the source was related to across the association,
/// named by descriptor (INV-8) with its projected id and the surviving edge
/// attributes.
/// </summary>
/// <param name="DestinationDescriptor">Descriptor name of the far endpoint's object type.</param>
/// <param name="DestinationId">Projected id of the far endpoint instance.</param>
public sealed record TraversalEndpoint(
    [property: JsonPropertyName("destinationDescriptor")] string DestinationDescriptor,
    [property: JsonPropertyName("destinationId")] string DestinationId)
{
    /// <summary>Edge-attribute values from the association object backing this hop.</summary>
    [JsonPropertyName("edgeAttributes")]
    public IReadOnlyDictionary<string, object?> EdgeAttributes { get; init; } =
        new Dictionary<string, object?>();
}

/// <summary>
/// Result of an instance-anchored traversal (DR-15). A successful result carries
/// the reached <see cref="Endpoints"/> and (when the page was truncated by the
/// self-imposed row budget) an opaque <see cref="NextCursor"/>; a validation
/// failure (closed-vocabulary violation, missing instance) carries
/// <see cref="IsError"/> = true and an <see cref="Error"/> message. Every result —
/// success or error — carries the INV-3 <c>_meta</c> envelope.
/// </summary>
/// <remarks>
/// The core tool returns this SDK-free shape; the hosting bridge maps a truncated
/// page to a <c>resource_link</c> content block and an <see cref="IsError"/> result
/// to a <c>CallToolResult</c> with <c>isError: true</c> (SEP-1303), never a thrown
/// protocol error.
/// </remarks>
public sealed record TraversalResult(
    [property: JsonPropertyName("_meta")] ResponseMeta Meta)
{
    /// <summary>The far endpoints reached by the traversal. Empty on an error result.</summary>
    [JsonPropertyName("endpoints")]
    public IReadOnlyList<TraversalEndpoint> Endpoints { get; init; } = [];

    /// <summary>True when the request was rejected by closed-vocabulary validation.</summary>
    [JsonPropertyName("isError")]
    public bool IsError { get; init; }

    /// <summary>Human-readable validation error; null on success.</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// Opaque continuation cursor when the page was truncated by the row budget;
    /// null when the full result fit. Carries its own schema — it is never a
    /// far-endpoint row.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextCursor { get; init; }

    /// <summary>
    /// True when the result was truncated and a <c>resource_link</c> to the full
    /// subgraph should be surfaced by the host instead of inlining every row.
    /// </summary>
    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }
}
