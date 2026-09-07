// -----------------------------------------------------------------------
// <copyright file="CompensationTopology.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Helpers;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Identifies the structural boundary that owns a compensation rollback.
/// </summary>
internal enum CompensationScopeKind
{
    /// <summary>The workflow root.</summary>
    Root = 0,

    /// <summary>One concrete loop iteration.</summary>
    LoopIteration = 1,

    /// <summary>The selected case of an exclusive branch.</summary>
    BranchPath = 2,

    /// <summary>A fork whose path lanes compensate after quiescence.</summary>
    Fork = 3,
}

/// <summary>Classifies the compatibility boundary of authored compensation.</summary>
internal enum CompensationProgramKind
{
    /// <summary>No compensation is configured.</summary>
    None = 0,

    /// <summary>Every compensation is the legacy runtime-only form.</summary>
    Legacy = 1,

    /// <summary>Every compensation carries a statically resolved inverse action.</summary>
    Typed = 2,

    /// <summary>Typed and legacy/dynamic declarations are mixed and must fail closed.</summary>
    Mixed = 3,
}

/// <summary>
/// Immutable structural scope metadata for one executable step occurrence.
/// </summary>
/// <param name="TemplateKey">
/// Stable scope template. Loop counters are represented as <c>{PropertyName}</c>
/// placeholders and are bound to the saga's persisted counter at dispatch time.
/// </param>
/// <param name="StructuralPath">The full structural path, including a fork lane.</param>
/// <param name="ParentTemplateKey">The enclosing compensation scope, if any.</param>
/// <param name="Kind">The kind of rollback boundary.</param>
/// <param name="Depth">The structural nesting depth (root is zero).</param>
/// <param name="LaneKey">The fork path lane, or null for sequential scopes.</param>
/// <param name="ForkId">The owning fork id, or null outside a fork.</param>
/// <param name="ForkPathIndex">The owning fork path index, or null outside a fork.</param>
internal sealed record CompensationScopeTemplate(
    string TemplateKey,
    string StructuralPath,
    string? ParentTemplateKey,
    CompensationScopeKind Kind,
    int Depth,
    string? LaneKey = null,
    string? ForkId = null,
    int? ForkPathIndex = null);

/// <summary>
/// One executable forward occurrence and its optional inverse.
/// </summary>
/// <param name="StableKey">Stable occurrence identity within the authored topology.</param>
/// <param name="PhaseName">The generated saga phase name.</param>
/// <param name="Step">The forward step model.</param>
/// <param name="Scope">The structural compensation scope.</param>
/// <param name="Ordinal">The forward order within the structural lane.</param>
/// <param name="PathKey">The identity-carrying fork/branch path key, if applicable.</param>
internal sealed record CompensationOccurrence(
    string StableKey,
    string PhaseName,
    StepModel Step,
    CompensationScopeTemplate Scope,
    int Ordinal,
    PathRoutingKey? PathKey = null)
{
    /// <summary>Gets whether the forward occurrence has a runnable inverse step.</summary>
    public bool HasInverse => Step.Compensation is not null;

    /// <summary>Gets the simple CLR name of the inverse step, if configured.</summary>
    public string? InverseStepName => Step.Compensation is null
        ? null
        : NamingHelper.GetSimpleTypeName(Step.Compensation.CompensationStepTypeName);

    /// <summary>Gets the stable language-neutral forward action identity.</summary>
    public string? ForwardActionIdentity => Step.Action is null
        ? null
        : string.Concat(
            Step.Action.DomainName,
            "/",
            Step.Action.ObjectTypeName,
            "/",
            Step.Action.ActionName);

    /// <summary>Gets the stable language-neutral inverse action identity.</summary>
    public string? InverseActionIdentity => Step.Compensation?.InverseIdentity;

    /// <summary>
    /// Gets whether this leaf is an identity-inverse candidate. The binding proof
    /// permits this shape only when the resolved forward action has an empty frame;
    /// workflows that do not prove that fact fail compilation before runtime.
    /// </summary>
    public bool UsesIdentityInverse => Step.Compensation is null
        && Step.Action is not null
        && Step.ActionResolution == WorkflowActionReferenceResolution.Resolved;
}

/// <summary>
/// Pure, immutable post-extraction projection of compensation-relevant topology.
/// </summary>
/// <remarks>
/// The parser remains the single owner of <see cref="WorkflowModel"/>. This projection
/// derives runtime rollback identities without mutating the shared generator IR. When a
/// collapsed phase cannot be assigned to one structural occurrence, <see cref="Issues"/>
/// records the ambiguity and lookup fails closed.
/// </remarks>
internal sealed class CompensationTopology
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CompensationOccurrence>> byPhase;
    private readonly IReadOnlyDictionary<PathRoutingKey, CompensationOccurrence> byPath;

    private CompensationTopology(
        IReadOnlyList<CompensationOccurrence> occurrences,
        IReadOnlyList<string> issues)
    {
        Occurrences = occurrences;
        Issues = issues;
        byPhase = occurrences
            .GroupBy(static occurrence => occurrence.PhaseName, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<CompensationOccurrence>)group.ToList(),
                StringComparer.Ordinal);
        byPath = occurrences
            .Where(static occurrence => occurrence.PathKey is not null)
            .GroupBy(static occurrence => occurrence.PathKey!.Value)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.First());
    }

    /// <summary>Gets all executable occurrences in deterministic structural order.</summary>
    public IReadOnlyList<CompensationOccurrence> Occurrences { get; }

    /// <summary>Gets topology ambiguities that prevent a sound runtime lookup.</summary>
    public IReadOnlyList<string> Issues { get; }

    /// <summary>Gets whether every occurrence lookup is structurally closed.</summary>
    public bool IsClosed => Issues.Count == 0;

    /// <summary>Gets the compatibility class of a workflow's compensation program.</summary>
    public static CompensationProgramKind GetProgramKind(WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));
        var compensations = model.Steps?
            .Where(static step => step.Compensation is not null)
            .Select(static step => step.Compensation!)
            .ToList() ?? [];
        if (compensations.Count == 0)
        {
            return CompensationProgramKind.None;
        }

        if (compensations.All(static compensation =>
            compensation.InverseActionResolution == WorkflowActionReferenceResolution.Missing))
        {
            return CompensationProgramKind.Legacy;
        }

        return compensations.All(static compensation =>
            compensation.InverseAction is not null
            && compensation.InverseActionResolution == WorkflowActionReferenceResolution.Resolved)
                ? CompensationProgramKind.Typed
                : CompensationProgramKind.Mixed;
    }

    /// <summary>Gets whether the whole program is statically typed.</summary>
    public static bool HasTypedCompensationProgram(WorkflowModel model) =>
        GetProgramKind(model) == CompensationProgramKind.Typed;

    /// <summary>
    /// Gets whether the derived runtime is required. Mixed programs enter it only
    /// so its whole-scope preflight can fail closed without executing a partial plan.
    /// </summary>
    public static bool UsesDerivedRuntime(WorkflowModel model) =>
        GetProgramKind(model) is CompensationProgramKind.Typed or CompensationProgramKind.Mixed;

    /// <summary>
    /// Builds a topology projection from the existing immutable workflow model.
    /// </summary>
    /// <param name="model">The extracted workflow model.</param>
    /// <returns>A deterministic compensation topology.</returns>
    public static CompensationTopology Build(WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var candidates = new List<CompensationOccurrence>();
        var issues = new List<string>();
        ValidateLoopIdentities(model, issues);
        AddForkOccurrences(model, candidates, issues);
        AddBranchOccurrences(model, candidates, issues);
        AddLoopOccurrences(model, candidates, issues);
        AddRootOccurrences(model, candidates, issues);

        // The same extracted phase may be visible through a containing construct as well
        // as its more-specific child. Keep the deepest structural occurrence. Equal-depth
        // duplicates are genuinely ambiguous and remain for fail-closed lookup.
        var deepestCandidates = candidates
            .GroupBy(static occurrence => occurrence.PhaseName, StringComparer.Ordinal)
            .SelectMany(group =>
            {
                var maxDepth = group.Max(static occurrence => occurrence.Scope.Depth);
                return group
                    .Where(occurrence => occurrence.Scope.Depth == maxDepth);
            })
            .ToList();

        foreach (var duplicate in deepestCandidates
            .GroupBy(static occurrence => occurrence.StableKey, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1))
        {
            AddIssue(issues, $"Stable compensation occurrence '{duplicate.Key}' is produced by more than one topology path.");
        }

        foreach (var duplicate in deepestCandidates
            .Where(static occurrence => occurrence.PathKey is not null)
            .GroupBy(static occurrence => occurrence.PathKey!.Value)
            .Where(static group => group.Count() > 1))
        {
            AddIssue(issues, $"Compensation routing key '{duplicate.Key}' is produced by more than one topology path.");
        }

        var deepest = deepestCandidates
            .GroupBy(static occurrence => occurrence.StableKey, StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static occurrence => occurrence.StableKey, StringComparer.Ordinal)
            .ToList();

        foreach (var group in deepest.GroupBy(static occurrence => occurrence.PhaseName, StringComparer.Ordinal))
        {
            if (group.Count() <= 1 || group.All(static occurrence => occurrence.PathKey is not null))
            {
                continue;
            }

            AddIssue(issues, $"Phase '{group.Key}' maps to multiple compensation occurrences without an identity-carrying path key.");
        }

        return new CompensationTopology(deepest, issues);
    }

    /// <summary>
    /// Resolves a phase occurrence, optionally with its identity-carrying path key.
    /// </summary>
    public bool TryResolve(
        string phaseName,
        PathRoutingKey? pathKey,
        out CompensationOccurrence occurrence)
    {
        if (!IsClosed)
        {
            occurrence = null!;
            return false;
        }

        if (pathKey is { } key && byPath.TryGetValue(key, out occurrence!))
        {
            return true;
        }

        if (byPhase.TryGetValue(phaseName, out var matches) && matches.Count == 1)
        {
            occurrence = matches[0];
            return true;
        }

        occurrence = null!;
        return false;
    }

    private static void AddRootOccurrences(
        WorkflowModel model,
        List<CompensationOccurrence> result,
        List<string> issues)
    {
        var mainFlow = MainFlowClassification.For(model);
        var stepsByPhase = BuildStepsByPhase(model, issues);
        var ordinal = 0;
        foreach (var phaseName in model.StepNames)
        {
            if (mainFlow.IsOffMainFlow(phaseName)
                || !stepsByPhase.TryGetValue(phaseName, out var step))
            {
                continue;
            }

            var scope = new CompensationScopeTemplate(
                "root",
                "root",
                null,
                CompensationScopeKind.Root,
                0);
            result.Add(CreateOccurrence(scope, step, phaseName, ordinal++, pathKey: null));
        }
    }

    private static void AddLoopOccurrences(
        WorkflowModel model,
        List<CompensationOccurrence> result,
        List<string> issues)
    {
        if (model.Loops is null)
        {
            return;
        }

        foreach (var loop in model.Loops.OrderBy(static value => value.FullPrefix, StringComparer.Ordinal))
        {
            if (!TryBuildLoopScope(model.Loops, loop, issues, out var scope))
            {
                continue;
            }

            for (var index = 0; index < loop.BodySteps.Count; index++)
            {
                var step = loop.BodySteps[index];
                result.Add(CreateOccurrence(scope, step, step.PhaseName, index, pathKey: null));
            }

            if (loop.BranchOnExit is not null)
            {
                AddBranch(model, loop.BranchOnExit, result, issues, parentTemplateOverride: scope.TemplateKey);
            }
        }
    }

    private static void AddBranchOccurrences(
        WorkflowModel model,
        List<CompensationOccurrence> result,
        List<string> issues)
    {
        if (model.Branches is null)
        {
            return;
        }

        foreach (var branch in model.Branches)
        {
            AddBranch(model, branch, result, issues, parentTemplateOverride: null);
        }
    }

    private static void AddBranch(
        WorkflowModel model,
        BranchModel branch,
        List<CompensationOccurrence> result,
        List<string> issues,
        string? parentTemplateOverride)
    {
        var stepsByPhase = BuildStepsByPhase(model, issues);
        string parentTemplate;
        int parentDepth;
        if (parentTemplateOverride is not null)
        {
            parentTemplate = parentTemplateOverride;
            parentDepth = TemplateDepth(parentTemplate);
        }
        else if (!TryResolveContainingLoopScope(
            model,
            branch.LoopPrefix,
            issues,
            out parentTemplate,
            out parentDepth))
        {
            return;
        }

        foreach (var branchCase in branch.Cases)
        {
            var caseSegment = $"branch:{branch.BranchId}/case:{branchCase.BranchPathPrefix}";
            var template = $"{parentTemplate}/{caseSegment}";
            var scope = new CompensationScopeTemplate(
                template,
                template,
                parentTemplate,
                CompensationScopeKind.BranchPath,
                parentDepth + 1);

            for (var index = 0; index < branchCase.StepNames.Count; index++)
            {
                var phaseName = string.IsNullOrEmpty(branch.LoopPrefix)
                    ? branchCase.StepNames[index]
                    : $"{branch.LoopPrefix}_{branchCase.StepNames[index]}";
                if (!stepsByPhase.TryGetValue(phaseName, out var step))
                {
                    continue;
                }

                var pathKey = PathRoutingKey.ForBranch(
                    branch.BranchId,
                    branchCase.BranchPathPrefix,
                    phaseName);
                result.Add(CreateOccurrence(scope, step, phaseName, index, pathKey));
            }
        }

        if (branch.NextConsecutiveBranch is not null)
        {
            AddBranch(model, branch.NextConsecutiveBranch, result, issues, parentTemplateOverride);
        }
    }

    private static void AddForkOccurrences(
        WorkflowModel model,
        List<CompensationOccurrence> result,
        List<string> issues)
    {
        if (model.Forks is null)
        {
            return;
        }

        foreach (var fork in model.Forks)
        {
            var loopPrefixes = fork.Paths
                .SelectMany(static path => path.Steps)
                .Select(static step => step.LoopName)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (loopPrefixes.Count > 1)
            {
                AddIssue(
                    issues,
                    $"Fork '{fork.ForkId}' has paths assigned to different containing loop scopes.");
                continue;
            }

            var loopPrefix = loopPrefixes.Count == 0 ? null : loopPrefixes[0];
            if (!TryResolveContainingLoopScope(
                model,
                loopPrefix,
                issues,
                out var loopParent,
                out var loopDepth))
            {
                continue;
            }

            var forkTemplate = $"{loopParent}/fork:{fork.ForkId}";
            foreach (var path in fork.Paths)
            {
                var laneKey = $"path:{path.PathIndex}";
                var structuralPath = $"{forkTemplate}/{laneKey}";
                var scope = new CompensationScopeTemplate(
                    forkTemplate,
                    structuralPath,
                    loopParent,
                    CompensationScopeKind.Fork,
                    loopDepth + 1,
                    laneKey,
                    fork.ForkId,
                    path.PathIndex);

                for (var index = 0; index < path.Steps.Count; index++)
                {
                    var step = path.Steps[index];
                    var pathKey = PathRoutingKey.ForFork(fork.ForkId, path.PathIndex, step.PhaseName);
                    result.Add(CreateOccurrence(scope, step, step.PhaseName, index, pathKey));
                }
            }
        }
    }

    private static CompensationOccurrence CreateOccurrence(
        CompensationScopeTemplate scope,
        StepModel step,
        string phaseName,
        int ordinal,
        PathRoutingKey? pathKey)
    {
        var stableKey = $"{scope.StructuralPath}/step:{phaseName}#{ordinal}";
        return new CompensationOccurrence(stableKey, phaseName, step, scope, ordinal, pathKey);
    }

    private static Dictionary<string, StepModel> BuildStepsByPhase(
        WorkflowModel model,
        List<string> issues)
    {
        var result = new Dictionary<string, StepModel>(StringComparer.Ordinal);
        if (model.Steps is null)
        {
            return result;
        }

        foreach (var group in model.Steps.GroupBy(static step => step.PhaseName, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
            {
                AddIssue(issues, $"Phase '{group.Key}' has more than one extracted step model.");
            }

            result[group.Key] = group.First();
        }

        return result;
    }

    private static void ValidateLoopIdentities(WorkflowModel model, List<string> issues)
    {
        if (model.Loops is null)
        {
            return;
        }

        foreach (var duplicate in model.Loops
            .GroupBy(static loop => loop.FullPrefix, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1))
        {
            AddIssue(issues, $"Loop prefix '{duplicate.Key}' identifies more than one loop.");
        }

        foreach (var duplicate in model.Loops
            .GroupBy(static loop => loop.IterationPropertyName, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1))
        {
            AddIssue(
                issues,
                $"Loop iteration property '{duplicate.Key}' is shared by more than one structural loop path.");
        }
    }

    private static bool TryResolveContainingLoopScope(
        WorkflowModel model,
        string? loopPrefix,
        List<string> issues,
        out string template,
        out int depth)
    {
        template = "root";
        depth = 0;
        if (string.IsNullOrEmpty(loopPrefix))
        {
            return true;
        }

        if (model.Loops is null)
        {
            AddIssue(issues, $"Containing loop '{loopPrefix}' has no extracted loop model.");
            return false;
        }

        var matches = ResolveLoopCandidates(model.Loops, loopPrefix);
        if (matches.Count != 1)
        {
            AddIssue(
                issues,
                $"Containing loop '{loopPrefix}' resolves to {matches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} structural loop paths.");
            return false;
        }

        if (!TryBuildLoopScope(model.Loops, matches[0], issues, out var scope))
        {
            return false;
        }

        template = scope.TemplateKey;
        depth = scope.Depth;
        return true;
    }

    private static bool TryBuildLoopScope(
        IReadOnlyList<LoopModel> loops,
        LoopModel loop,
        List<string> issues,
        out CompensationScopeTemplate scope)
    {
        var reverseHierarchy = new List<LoopModel>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = loop;
        while (true)
        {
            var structuralIdentity = string.Concat(current.ConditionId, "\u001f", current.FullPrefix);
            if (!visited.Add(structuralIdentity))
            {
                AddIssue(issues, $"Loop '{loop.FullPrefix}' contains a parent cycle.");
                scope = null!;
                return false;
            }

            reverseHierarchy.Add(current);
            if (string.IsNullOrEmpty(current.ParentLoopName))
            {
                break;
            }

            var parents = ResolveLoopCandidates(loops, current.ParentLoopName);
            if (parents.Count != 1)
            {
                AddIssue(
                    issues,
                    $"Parent '{current.ParentLoopName}' of loop '{current.FullPrefix}' resolves to {parents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} structural paths.");
                scope = null!;
                return false;
            }

            current = parents[0];
        }

        reverseHierarchy.Reverse();
        var template = "root";
        var parentTemplate = "root";
        foreach (var member in reverseHierarchy)
        {
            parentTemplate = template;
            template = $"{template}/loop:{member.LoopName}@{{{member.IterationPropertyName}}}";
        }

        scope = new CompensationScopeTemplate(
            template,
            template,
            parentTemplate,
            CompensationScopeKind.LoopIteration,
            reverseHierarchy.Count);
        return true;
    }

    private static IReadOnlyList<LoopModel> ResolveLoopCandidates(
        IReadOnlyList<LoopModel> loops,
        string identity)
    {
        var exactPaths = loops
            .Where(loop => string.Equals(loop.FullPrefix, identity, StringComparison.Ordinal))
            .ToList();
        if (exactPaths.Count > 0)
        {
            return exactPaths;
        }

        return loops
            .Where(loop => string.Equals(loop.LoopName, identity, StringComparison.Ordinal))
            .ToList();
    }

    private static void AddIssue(List<string> issues, string issue)
    {
        if (!issues.Contains(issue, StringComparer.Ordinal))
        {
            issues.Add(issue);
        }
    }

    private static int TemplateDepth(string template) =>
        string.Equals(template, "root", StringComparison.Ordinal)
            ? 0
            : template.Count(static character => character == '/');
}
