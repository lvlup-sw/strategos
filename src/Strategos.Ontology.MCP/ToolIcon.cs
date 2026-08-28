using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Optional MCP tool icon (2026-07-28 <c>Tool.icons</c>). Mirrors the protocol
/// <c>Icon</c> shape so the core assembly can describe icons without taking a
/// ModelContextProtocol dependency (INV-2). Null on the descriptor when the
/// source supplies none — do not invent a placeholder.
/// </summary>
/// <param name="Source">URI of the icon resource (https or data URI).</param>
public sealed record ToolIcon(
    [property: JsonPropertyName("src")] string Source)
{
    /// <summary>Optional MIME type override (e.g. <c>image/png</c>).</summary>
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    /// <summary>Optional size tokens such as <c>48x48</c> or <c>any</c>.</summary>
    [JsonPropertyName("sizes")]
    public IReadOnlyList<string>? Sizes { get; init; }

    /// <summary>Optional theme hint: <c>light</c> or <c>dark</c>.</summary>
    [JsonPropertyName("theme")]
    public string? Theme { get; init; }
}
