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

    /// <summary>
    /// Indicates whether the action is read-only. When <c>true</c>, the action
    /// is dispatchable via
    /// <see cref="Strategos.Ontology.Actions.IActionDispatcher.DispatchReadOnlyAsync"/>
    /// and must not declare write postconditions. Defaults to <c>false</c>.
    /// </summary>
    public bool IsReadOnly { get; init; }

    public IReadOnlyList<ActionPrecondition> Preconditions { get; init; } = [];

    public IReadOnlyList<ActionPostcondition> Postconditions { get; init; } = [];
}
