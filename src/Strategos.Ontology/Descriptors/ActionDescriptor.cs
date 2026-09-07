using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

public sealed record ActionDescriptor
{
    private ImmutableArray<ActionResource> touchedResources = [];
    private ImmutableArray<string> allowedClients = [];
    private ImmutableArray<ActionPrecondition> preconditions = [];
    private ImmutableArray<ActionGuarantee> ensures = [];
    private ImmutableArray<ActionPostcondition> postconditions = [];

    /// <summary>Initializes an action descriptor with stable ontology identity.</summary>
    public ActionDescriptor(ActionSubject subject, string name, string description)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Action name cannot be empty.", nameof(name));
        }

        Name = name;
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>Gets the ontology object this action operates on.</summary>
    public ActionSubject Subject { get; }

    /// <summary>Gets the action name within its subject.</summary>
    public string Name { get; }

    /// <summary>Gets the human-readable description.</summary>
    public string Description { get; }

    public Type? AcceptsType { get; init; }

    public Type? ReturnsType { get; init; }

    public ActionBindingType BindingType { get; init; } = ActionBindingType.Unbound;

    public WorkflowBindingReference? BoundWorkflow { get; init; }

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
    public IReadOnlyList<string> AllowedClients
    {
        get => allowedClients;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            allowedClients = value.ToImmutableArray();
        }
    }

    /// <summary>
    /// Indicates that an interactive client must obtain confirmation before
    /// dispatching this action.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Typed preconditions for this action.
    /// </summary>
    public IReadOnlyList<ActionPrecondition> Preconditions
    {
        get => preconditions;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            preconditions = value.ToImmutableArray();
        }
    }

    /// <summary>Explicit post-state facts promised on successful completion.</summary>
    public IReadOnlyList<ActionGuarantee> Ensures
    {
        get => ensures;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            ensures = value.ToImmutableArray();
        }
    }

    /// <summary>Effect and frame metadata; these are not value guarantees.</summary>
    public IReadOnlyList<ActionPostcondition> Postconditions
    {
        get => postconditions;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            postconditions = value.ToImmutableArray();
        }
    }
}
