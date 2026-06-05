namespace Strategos.Ontology.Descriptors;

public sealed record ExternalLinkExtensionPoint
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? RequiredSourceInterface { get; init; }

    public string? RequiredSourceDomain { get; init; }

    public int? MaxLinks { get; init; }

    public IReadOnlyList<string> MatchedLinkNames { get; init; } = [];
}
