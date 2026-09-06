using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Strategos.Ontology.MCP;

/// <summary>
/// Projects one ontology action contract into the dynamic
/// <c>ontology_action</c> tool descriptor.
/// </summary>
public sealed record ActionSemanticSummary(
    string DomainName,
    string ObjectTypeName,
    string ActionName,
    ToolAnnotations Annotations,
    IReadOnlyList<ActionAuthorizationRequirement> AuthorizationRequirements,
    string? RequiredAuthority,
    IReadOnlyList<string> AllowedClients,
    bool RequiresConfirmation,
    IReadOnlyList<Strategos.Ontology.Descriptors.ActionResource> TouchedResources,
    string? CompensatingActionName)
{
    /// <summary>Typed hard and soft requirements in the Contracts 0.10 wire shape.</summary>
    [JsonPropertyName("requires")]
    public ImmutableArray<ActionRequirementV1> Requires { get; init; } = [];

    /// <summary>Explicit post-state guarantees in the Contracts 0.10 wire shape.</summary>
    [JsonPropertyName("ensures")]
    public ImmutableArray<ActionGuaranteeV1> Ensures { get; init; } = [];
}
