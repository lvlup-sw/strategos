using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// The immutable set of resources an action may affect. Predicates outside the
/// frame retain their truth value across the action.
/// </summary>
public sealed class ActionFrame
{
    public ActionFrame(IEnumerable<ActionResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Resources = resources
            .Distinct()
            .OrderBy(resource => resource.Kind)
            .ThenBy(resource => resource.Name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public static ActionFrame Empty { get; } = new([]);

    public ImmutableArray<ActionResource> Resources { get; }

    public bool Contains(ActionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return Resources.Contains(resource);
    }

    public bool IsDisjointFrom(ActionFrame other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !Resources.Intersect(other.Resources).Any();
    }

    public ActionFrame Union(ActionFrame other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new ActionFrame(Resources.Concat(other.Resources));
    }
}
