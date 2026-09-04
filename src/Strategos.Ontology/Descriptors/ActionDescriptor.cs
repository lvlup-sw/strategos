using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

public sealed record ActionDescriptor(
    string Name,
    string Description)
{
    private ImmutableArray<ActionResource> touchedResources = [];

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

    /// <summary>
    /// Indicates whether repeating the action produces the same externally
    /// observable effect. Defaults to <c>false</c>. Read-only actions must also
    /// be idempotent.
    /// </summary>
    public bool Idempotent { get; init; }

    /// <summary>
    /// Named authority literal required to invoke this action. Null means the
    /// action declares no authority requirement.
    /// </summary>
    public string? RequiredAuthority { get; init; }

    /// <summary>
    /// Resources this action may affect. The frame must contain every resource
    /// named by a mutating postcondition.
    /// </summary>
    public IReadOnlyList<ActionResource> TouchedResources
    {
        get => touchedResources;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            touchedResources = value.ToImmutableArray();
        }
    }

    /// <summary>Name of the action that restores this action's frame.</summary>
    public string? CompensatingActionName { get; init; }

    /// <summary>
    /// Client identifiers allowed to surface this action. An empty collection
    /// means the contract does not restrict discovery by client.
    /// </summary>
    public IReadOnlyList<string> AllowedClients { get; init; } = [];

    /// <summary>
    /// Indicates that an interactive client must obtain confirmation before
    /// dispatching this action.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Descriptor-first preconditions for this action. This is the
    /// first-class authoring field; the fluent
    /// <c>IActionBuilder&lt;T&gt;.Requires(...)</c> methods are obsolete
    /// and have no fluent successor.
    /// </summary>
    public IReadOnlyList<ActionPrecondition> Preconditions { get; init; } = [];

    public IReadOnlyList<ActionPostcondition> Postconditions { get; init; } = [];
}
