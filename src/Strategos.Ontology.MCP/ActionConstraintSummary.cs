namespace Strategos.Ontology.MCP;

/// <summary>
/// Summarizes the constraints on an ontology action, giving LLM agents
/// richer semantic context for tool selection.
/// </summary>
public sealed record ActionConstraintSummary(
    string ObjectTypeName,
    string ActionName,
    int HardConstraintCount,
    int SoftConstraintCount,
    IReadOnlyList<string> ConstraintDescriptions);
