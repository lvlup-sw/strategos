namespace Strategos.Ontology.MCP;

/// <summary>
/// Describes the relation path an authenticated principal must satisfy before
/// an ontology action can be dispatched.
/// </summary>
public sealed record ActionAuthorizationRequirement(
    string RelationName,
    IReadOnlyList<string> LinkPath);
