namespace Strategos.Ontology.MCP;

/// <summary>
/// Per-response metadata threaded through every ontology MCP tool result.
/// Consumers use <see cref="OntologyVersion"/> to invalidate schema caches
/// when the ontology graph mutates (Strategos 2.6.0 hybrid retrieval cache;
/// Exarchos client-side schema cache).
///
/// Design: docs/designs/2026-04-19-mcp-surface-conformance.md §4.5
/// </summary>
public sealed record ResponseMeta(string OntologyVersion);
