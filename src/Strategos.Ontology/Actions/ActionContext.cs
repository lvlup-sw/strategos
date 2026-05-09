using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Identifies the target of an ontology action invocation.
/// </summary>
/// <param name="Domain">Domain that owns the target object type.</param>
/// <param name="ObjectType">Simple object type name within the domain.</param>
/// <param name="ObjectId">Identifier of the specific instance the action targets.</param>
/// <param name="ActionName">Name of the action to invoke.</param>
/// <param name="Options">Optional dispatch options that influence routing or hooks.</param>
public sealed record ActionContext(
    string Domain,
    string ObjectType,
    string ObjectId,
    string ActionName,
    ActionDispatchOptions? Options = null)
{
    /// <summary>
    /// Optional resolved descriptor for the action being dispatched. When supplied,
    /// the dispatch path can apply descriptor-driven guards (such as the read-only
    /// invariant enforced by <see cref="IActionDispatcher.DispatchReadOnlyAsync"/>)
    /// without re-resolving against the ontology graph.
    /// </summary>
    public ActionDescriptor? ActionDescriptor { get; init; }
}
