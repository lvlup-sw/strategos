using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Identifies the authenticated principal and target of an ontology action invocation.
/// </summary>
public sealed record ActionContext
{
    private readonly ActionPrincipal principal = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionContext"/> class.
    /// </summary>
    /// <param name="principal">Authenticated principal requesting the action.</param>
    /// <param name="domain">Domain that owns the target object type.</param>
    /// <param name="objectType">Simple object type name within the domain.</param>
    /// <param name="objectId">Identifier of the specific instance the action targets.</param>
    /// <param name="actionName">Name of the action to invoke.</param>
    /// <param name="options">Optional dispatch options that influence routing or hooks.</param>
    public ActionContext(
        ActionPrincipal principal,
        string domain,
        string objectType,
        string objectId,
        string actionName,
        ActionDispatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        Principal = principal;
        Domain = domain;
        ObjectType = objectType;
        ObjectId = objectId;
        ActionName = actionName;
        Options = options;
    }

    /// <summary>
    /// Gets the authenticated principal requesting the action.
    /// </summary>
    public ActionPrincipal Principal
    {
        get => principal;
        init => principal = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the domain that owns the target object type.
    /// </summary>
    public string Domain { get; init; }

    /// <summary>
    /// Gets the simple object type name within the domain.
    /// </summary>
    public string ObjectType { get; init; }

    /// <summary>
    /// Gets the identifier of the specific target instance.
    /// </summary>
    public string ObjectId { get; init; }

    /// <summary>
    /// Gets the name of the action to invoke.
    /// </summary>
    public string ActionName { get; init; }

    /// <summary>
    /// Gets dispatch options that influence routing or hooks.
    /// </summary>
    public ActionDispatchOptions? Options { get; init; }

    /// <summary>
    /// Optional resolved descriptor for the action being dispatched. When supplied,
    /// the dispatch path can apply descriptor-driven guards (such as the read-only
    /// invariant enforced by <see cref="IActionDispatcher.DispatchReadOnlyAsync"/>)
    /// without re-resolving against the ontology graph.
    /// </summary>
    public ActionDescriptor? ActionDescriptor { get; init; }
}
