using Strategos.Ontology.Descriptors;

namespace Strategos.Ontology.Actions;

/// <summary>
/// Identifies the target of an ontology action invocation.
/// </summary>
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
