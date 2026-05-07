using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of an ontology exploration query.
/// </summary>
public sealed record ExploreResult(
    string Scope,
    IReadOnlyList<Dictionary<string, object?>> Items,
    [property: JsonPropertyName("_meta")] ResponseMeta Meta);
