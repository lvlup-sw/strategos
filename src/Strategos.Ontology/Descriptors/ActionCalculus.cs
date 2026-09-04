using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>Pure composition operations over action contracts.</summary>
public static class ActionCalculus
{
    /// <summary>
    /// Computes sequential composition. Frames are unioned and authority
    /// requirements are joined; neither composite value is authored separately.
    /// </summary>
    public static CompositeActionContract Sequential(
        AuthorityLattice authorityLattice,
        params ActionDescriptor[] actions)
    {
        ArgumentNullException.ThrowIfNull(authorityLattice);
        ArgumentNullException.ThrowIfNull(actions);

        var frame = actions.Aggregate(
            ActionFrame.Empty,
            (current, action) => current.Union(new ActionFrame(action.TouchedResources)));
        var requiredAuthorities = actions
            .Select(action => action.RequiredAuthority)
            .Where(authority => authority is not null)
            .Cast<string>();

        return new CompositeActionContract(
            actions.ToImmutableArray(),
            authorityLattice.Join(requiredAuthorities),
            frame);
    }

    /// <summary>
    /// Derives the rollback order for a completed forward prefix.
    /// </summary>
    public static ImmutableArray<string> DeriveRollbackPlan(
        IEnumerable<ActionDescriptor> completedForwardPrefix)
    {
        ArgumentNullException.ThrowIfNull(completedForwardPrefix);
        var actions = completedForwardPrefix.ToArray();
        if (actions.Any(action => string.IsNullOrWhiteSpace(action.CompensatingActionName)))
        {
            throw new InvalidOperationException(
                "Every completed action must name a compensating action before a rollback plan can be derived.");
        }

        return actions
            .Reverse()
            .Select(action => action.CompensatingActionName!)
            .ToImmutableArray();
    }

    /// <summary>Checks an authored rollback sequence against the derived plan.</summary>
    public static bool AuthoredRollbackAgrees(
        IEnumerable<ActionDescriptor> completedForwardPrefix,
        IEnumerable<string> authoredRollback)
    {
        ArgumentNullException.ThrowIfNull(authoredRollback);
        return DeriveRollbackPlan(completedForwardPrefix)
            .SequenceEqual(authoredRollback, StringComparer.Ordinal);
    }
}
