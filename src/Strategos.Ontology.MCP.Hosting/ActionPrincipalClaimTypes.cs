namespace Strategos.Ontology.MCP.Hosting;

/// <summary>
/// Claim names consumed by the default ontology action-principal resolver.
/// </summary>
public static class ActionPrincipalClaimTypes
{
    /// <summary>
    /// Claim whose value names the ontology descriptor type of the caller.
    /// </summary>
    public const string PrincipalType = "strategos:principal_type";

    /// <summary>Repeatable claim containing a named authority grant.</summary>
    public const string Authority = "strategos:authority";
}
