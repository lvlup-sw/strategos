using System.Text.Json;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Describes an MCP tool exposed by the ontology layer. Aligned with the
/// MCP 2025-11-25 tool descriptor shape (Title / OutputSchema / Annotations
/// supplement the legacy Name+Description pair).
/// </summary>
public sealed record OntologyToolDescriptor(
    string Name,
    string Description)
{
    /// <summary>
    /// Human-readable display label. The MCP spec's intent for <c>title</c>
    /// is the form a client like Cursor renders in its tool picker.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// JSON-Schema description of the tool's result shape. Lets external
    /// agents validate responses and author follow-up queries deterministically.
    /// </summary>
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// Tool annotations consumed by MCP clients to gate auto-approval, batching,
    /// and caching decisions. Defaults are all-false (no hints, ask the user).
    /// </summary>
    public ToolAnnotations Annotations { get; init; } =
        new(ReadOnlyHint: false, DestructiveHint: false, IdempotentHint: false, OpenWorldHint: false);

    /// <summary>
    /// Constraint summaries for actions discovered from the ontology graph.
    /// Populated only for the ontology_action tool descriptor.
    /// </summary>
    public IReadOnlyList<ActionConstraintSummary> ConstraintSummaries { get; init; } = [];
}
