using System.Security.Claims;

using Strategos.Ontology.Actions;

namespace Strategos.Ontology.MCP.Hosting;

/// <summary>
/// Resolves an ontology action principal from an authenticated MCP caller.
/// </summary>
public interface IActionPrincipalResolver
{
    /// <summary>
    /// Resolves <paramref name="caller"/> to an ontology principal, or returns
    /// <c>null</c> when the caller cannot be bound safely.
    /// </summary>
    /// <param name="caller">Authenticated caller supplied by the MCP transport.</param>
    /// <returns>The bound ontology principal, or <c>null</c> to refuse dispatch.</returns>
    ActionPrincipal? Resolve(ClaimsPrincipal caller);
}
