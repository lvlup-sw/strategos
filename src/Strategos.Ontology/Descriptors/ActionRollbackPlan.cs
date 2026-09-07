using System.Collections.Immutable;

namespace Strategos.Ontology.Descriptors;

/// <summary>The closed structural variants of a mechanically derived rollback plan.</summary>
public enum ActionRollbackPlanKind
{
    /// <summary>The empty plan for one subject.</summary>
    Identity,

    /// <summary>The inverse of one completed action.</summary>
    Leaf,

    /// <summary>Rollback children executed in their stored order.</summary>
    Sequence,

    /// <summary>Rollback branches executed in parallel.</summary>
    Parallel,

    /// <summary>A preserved nested compensation boundary.</summary>
    Scope,
}

/// <summary>Stable executable identity and proof state for one rollback leaf.</summary>
public sealed record ActionRollbackLeaf
{
    internal ActionRollbackLeaf(ActionInverseAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ForwardAction = new ActionContractIdentity(
            analysis.ForwardAction.Subject,
            analysis.ForwardAction.Name);
        InverseAction = analysis.Status != ActionInverseAnalysisStatus.Proven
            || analysis.AuthoredInverse is null
            ? null
            : new ActionContractIdentity(
                analysis.AuthoredInverse.Subject,
                analysis.AuthoredInverse.Name);
        Status = analysis.Status;
        UsesIdentityInverse = analysis.UsesIdentityInverse;
        Failures = analysis.Failures;
    }

    /// <summary>Gets the completed forward action.</summary>
    public ActionContractIdentity ForwardAction { get; }

    /// <summary>Gets the authored inverse to execute, when one is required and proved.</summary>
    public ActionContractIdentity? InverseAction { get; }

    /// <summary>Gets the leaf inverse proof outcome.</summary>
    public ActionInverseAnalysisStatus Status { get; }

    /// <summary>Gets whether this leaf executes the distinct empty identity inverse.</summary>
    public bool UsesIdentityInverse { get; }

    /// <summary>Gets deterministic proof failures for a non-compensable leaf.</summary>
    public ImmutableArray<ActionInverseFailure> Failures { get; }

    /// <summary>Gets whether this leaf has an executable, proved inverse.</summary>
    public bool IsCompensable => Status == ActionInverseAnalysisStatus.Proven;
}

/// <summary>
/// Immutable rollback syntax tree derived from the completed portion of a forward program.
/// </summary>
public sealed class ActionRollbackPlan
{
    internal ActionRollbackPlan(
        ActionRollbackPlanKind kind,
        ActionSubject subject,
        ActionRollbackLeaf? leaf,
        IEnumerable<ActionRollbackPlan> children)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The rollback plan kind is not defined.");
        }

        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        ArgumentNullException.ThrowIfNull(children);
        var childArray = children.ToImmutableArray();
        if (childArray.Any(child => child is null))
        {
            throw new ArgumentException(
                "Rollback plan children cannot contain null entries.",
                nameof(children));
        }

        if (childArray.Any(child => !subject.Equals(child.Subject)))
        {
            throw new ArgumentException(
                "Every rollback child must operate on the plan subject.",
                nameof(children));
        }

        var validShape = kind switch
        {
            ActionRollbackPlanKind.Identity => leaf is null && childArray.IsEmpty,
            ActionRollbackPlanKind.Leaf => leaf is not null && childArray.IsEmpty,
            ActionRollbackPlanKind.Sequence => leaf is null && !childArray.IsEmpty,
            ActionRollbackPlanKind.Parallel => leaf is null && !childArray.IsEmpty,
            ActionRollbackPlanKind.Scope => leaf is null && childArray.Length == 1,
            _ => false,
        };
        if (!validShape)
        {
            throw new ArgumentException(
                $"Rollback plan kind '{kind}' has an invalid structural shape.",
                nameof(kind));
        }

        Kind = kind;
        Leaf = leaf;
        Children = childArray;
        IsCompensable = kind == ActionRollbackPlanKind.Identity
            || (leaf?.IsCompensable ?? childArray.All(child => child.IsCompensable));
        NonCompensableLeaves = kind == ActionRollbackPlanKind.Leaf && leaf is { IsCompensable: false }
            ? [leaf]
            : childArray.SelectMany(child => child.NonCompensableLeaves).ToImmutableArray();
    }

    /// <summary>Gets the structural plan variant.</summary>
    public ActionRollbackPlanKind Kind { get; }

    /// <summary>Gets the single ontology subject shared by the plan.</summary>
    public ActionSubject Subject { get; }

    /// <summary>Gets leaf execution data when <see cref="Kind"/> is <see cref="ActionRollbackPlanKind.Leaf"/>.</summary>
    public ActionRollbackLeaf? Leaf { get; }

    /// <summary>Gets nested plans in rollback execution order.</summary>
    public ImmutableArray<ActionRollbackPlan> Children { get; }

    /// <summary>Gets whether every leaf in this plan has an executable, proved inverse.</summary>
    public bool IsCompensable { get; }

    /// <summary>Gets non-compensable leaves in deterministic rollback traversal order.</summary>
    public ImmutableArray<ActionRollbackLeaf> NonCompensableLeaves { get; }
}
