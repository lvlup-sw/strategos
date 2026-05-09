using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Response envelope for the ontology_validate MCP tool. Wraps a
/// <see cref="ValidationVerdict"/> with the standard <c>_meta</c> envelope
/// carrying the ontology version, mirroring <see cref="QueryResult"/> /
/// <see cref="ExploreResult"/> / <see cref="ActionToolResult"/>.
/// </summary>
public sealed record ValidateResult(
    ValidationVerdict Verdict,
    [property: JsonPropertyName("_meta")] ResponseMeta Meta);
