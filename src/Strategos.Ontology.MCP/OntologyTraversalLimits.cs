namespace Strategos.Ontology.MCP;

/// <summary>
/// Bounds the MCP ontology traversal surface owns (DR-15). These are the MCP
/// layer's OWN limits, deliberately not a reference to any provider-internal
/// budget — the in-process and Npgsql providers enforce their own join/CTE
/// depth tiers, while the MCP tool caps the agent-facing closed vocabulary here.
/// </summary>
public static class OntologyTraversalLimits
{
    /// <summary>
    /// Maximum traversal depth accepted by the MCP traversal tool, matching the
    /// documented join-chain budget. Depth beyond this is rejected as a structured
    /// validation error (closed vocabulary), never executed.
    /// </summary>
    public const int MaxDepth = 3;
}
