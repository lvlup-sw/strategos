namespace Strategos.Ontology.Descriptors;

public sealed record ActionDescriptor(
    string Name,
    string Description)
{
    public Type? AcceptsType { get; init; }

    public Type? ReturnsType { get; init; }

    public ActionBindingType BindingType { get; init; } = ActionBindingType.Unbound;

    public string? BoundWorkflowName { get; init; }

    public string? BoundToolName { get; init; }

    public string? BoundToolMethod { get; init; }

    public bool IsReadOnly { get; init; }

    public IReadOnlyList<ActionPrecondition> Preconditions { get; init; } = [];

    public IReadOnlyList<ActionPostcondition> Postconditions { get; init; } = [];
}
