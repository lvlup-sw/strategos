namespace Strategos.Ontology.MCP;

/// <summary>
/// MCP 2025-11-25 tool annotations. Booleans are hints to the client about
/// the tool's behavior so the client can gate auto-approval, batching, and
/// caching decisions. Authoritative reference: MCP spec, server/tools.
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.2
/// </summary>
public sealed record ToolAnnotations(
    bool ReadOnlyHint,
    bool DestructiveHint,
    bool IdempotentHint,
    bool OpenWorldHint);
