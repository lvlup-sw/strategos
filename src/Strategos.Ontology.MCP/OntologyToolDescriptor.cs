using System.Text.Json;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Describes an MCP tool exposed by the ontology layer. Carries the MCP
/// 2025-11-25 surface — name, description, optional title, output JSON schema,
/// behavior annotations, and any action constraint summaries discovered from
/// the ontology graph.
///
/// Backward-compatible: existing two-arg construction <c>new(name, description)</c>
/// still compiles and produces all-default new fields (null Title/OutputSchema,
/// all-false Annotations, empty ConstraintSummaries).
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.3
/// </summary>
public sealed record OntologyToolDescriptor(
    string Name,
    string Description)
{
    /// <summary>
    /// Human-readable display label. MCP clients (Cursor, Copilot, Codex)
    /// render this in their tool picker.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// JSON Schema describing the tool's response shape. Generated from the
    /// concrete result record via <c>JsonSchemaHelper.JsonSchemaFor&lt;T&gt;()</c>
    /// at discovery time.
    /// </summary>
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// MCP 2025-11-25 behavior hints. Defaults to all-false (the spec-safe
    /// default — "no hints, ask the user").
    /// </summary>
    public ToolAnnotations Annotations { get; init; } =
        new(ReadOnlyHint: false, DestructiveHint: false,
            IdempotentHint: false, OpenWorldHint: false);

    /// <summary>
    /// Constraint summaries for actions discovered from the ontology graph.
    /// Populated only for the ontology_action tool descriptor.
    /// </summary>
    public IReadOnlyList<ActionConstraintSummary> ConstraintSummaries { get; init; } = [];
}
