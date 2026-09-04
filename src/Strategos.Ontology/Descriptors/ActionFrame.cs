using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>
/// The immutable set of resources an action may affect. Predicates outside the
/// frame retain their truth value across the action.
/// </summary>
public sealed class ActionFrame
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionFrame"/> class.
    /// </summary>
    /// <param name="resources">The resources the action may affect.</param>
    public ActionFrame(IEnumerable<ActionResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Resources = resources
            .Distinct()
            .OrderBy(resource => resource.Kind)
            .ThenBy(resource => resource.Name, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Gets the frame that affects no resources.</summary>
    public static ActionFrame Empty { get; } = new([]);

    /// <summary>Gets the distinct resources in canonical order.</summary>
    public ImmutableArray<ActionResource> Resources { get; }

    /// <summary>Determines whether this frame contains the specified resource.</summary>
    /// <param name="resource">The resource to locate.</param>
    /// <returns><see langword="true"/> when the resource is in the frame.</returns>
    public bool Contains(ActionResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return Resources.Contains(resource);
    }

    /// <summary>Determines whether this frame and another frame affect no common resource.</summary>
    /// <param name="other">The other frame.</param>
    /// <returns><see langword="true"/> when the frames are disjoint.</returns>
    public bool IsDisjointFrom(ActionFrame other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return !Resources.Intersect(other.Resources).Any();
    }

    /// <summary>Creates the union of this frame and another frame.</summary>
    /// <param name="other">The other frame.</param>
    /// <returns>A canonical frame containing both resource sets.</returns>
    public ActionFrame Union(ActionFrame other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new ActionFrame(Resources.Concat(other.Resources));
    }
}
