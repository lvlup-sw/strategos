namespace Strategos.Ontology.MCP;

/// <summary>
/// Projects one ontology action contract into the dynamic
/// <c>ontology_action</c> tool descriptor.
/// </summary>
public sealed record ActionSemanticSummary(
    string ObjectTypeName,
    string ActionName,
    ToolAnnotations Annotations,
    IReadOnlyList<ActionAuthorizationRequirement> AuthorizationRequirements,
    string? RequiredAuthority,
    IReadOnlyList<string> AllowedClients,
    bool RequiresConfirmation,
    IReadOnlyList<Strategos.Ontology.Descriptors.ActionResource> TouchedResources,
    string? CompensatingActionName);
