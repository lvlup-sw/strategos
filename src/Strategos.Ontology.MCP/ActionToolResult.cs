using Strategos.Ontology.Actions;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Result of an ontology action execution.
/// </summary>
public sealed record ActionToolResult(
    IReadOnlyList<ActionResult> Results);
