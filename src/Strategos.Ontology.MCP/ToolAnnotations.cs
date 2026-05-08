namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP 2025-11-25 tool annotations. Booleans are hints to the client about
/// the tool's behavior so the client can gate auto-approval, batching, and
/// caching decisions.
/// </summary>
/// <param name="ReadOnlyHint">If true, the tool does not modify state.</param>
/// <param name="DestructiveHint">If true, the tool may make destructive changes.</param>
/// <param name="IdempotentHint">If true, repeating the call yields the same effect.</param>
/// <param name="OpenWorldHint">If true, the tool may access resources beyond the registered surface.</param>
public sealed record ToolAnnotations(
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);
