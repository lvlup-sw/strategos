using System.Security.Claims;

using Strategos.Ontology.Actions;

namespace Strategos.Ontology.MCP.Hosting;

/// <summary>
/// Resolves MCP callers from standard subject identifiers plus the Strategos
/// ontology principal-type claim.
/// </summary>
public sealed class ClaimsActionPrincipalResolver : IActionPrincipalResolver
{
    /// <summary>
    /// Shared stateless resolver used when a host does not register a custom resolver.
    /// </summary>
    public static ClaimsActionPrincipalResolver Instance { get; } = new();

    /// <inheritdoc />
    public ActionPrincipal? Resolve(ClaimsPrincipal caller)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (caller.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return null;
        }

        var principalType = identity.FindFirst(ActionPrincipalClaimTypes.PrincipalType)?.Value;
        var principalId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? identity.FindFirst("sub")?.Value;

        return string.IsNullOrWhiteSpace(principalType) || string.IsNullOrWhiteSpace(principalId)
            ? null
            : new ActionPrincipal(principalType, principalId);
    }
}
