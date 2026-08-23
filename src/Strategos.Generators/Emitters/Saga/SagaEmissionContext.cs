// -----------------------------------------------------------------------
// <copyright file="SagaEmissionContext.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Provides pre-computed lookup context for saga emission.
/// </summary>
/// <remarks>
/// <para>
/// This class encapsulates all the lookup dictionaries needed for saga emission,
/// computing them once at creation time to avoid redundant calculations.
/// </para>
/// <para>
/// The lookups include:
/// <list type="bullet">
/// <item><description>Loops keyed by their last body step (for loop completion handlers)</description></item>
/// <item><description>Branches keyed by their previous step (for branch routing)</description></item>
/// <item><description>Branch path info for non-terminal branches (for path end handlers)</description></item>
/// <item><description>Steps keyed by name (for validation info)</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class SagaEmissionContext
{
    /// <summary>
    /// Gets the workflow model.
    /// </summary>
    public WorkflowModel Model { get; }

    /// <summary>
    /// Gets the computed saga class name.
    /// </summary>
    public string SagaClassName { get; }

    /// <summary>
    /// Gets the loops indexed by their last body step name.
    /// </summary>
    /// <remarks>
    /// For nested loops, multiple loops can end at the same step.
    /// The list is ordered from innermost to outermost (innermost first).
    /// </remarks>
    public IReadOnlyDictionary<string, IReadOnlyList<LoopModel>> LoopsByLastStep { get; }

    /// <summary>
    /// Gets the branches indexed by their previous step name.
    /// </summary>
    public IReadOnlyDictionary<string, BranchModel> BranchesByPreviousStep { get; }

    /// <summary>
    /// Gets the branch path info indexed by the last step of each non-terminal branch path.
    /// </summary>
    public IReadOnlyDictionary<string, (BranchModel Branch, BranchCaseModel Case)> BranchPathInfo { get; }

    /// <summary>
    /// Gets the step models indexed by step name.
    /// </summary>
    public IReadOnlyDictionary<string, StepModel> StepsByName { get; }

    /// <summary>
    /// Gets the approval models indexed by their preceding step name.
    /// </summary>
    /// <remarks>
    /// This lookup allows determining if a step has an approval checkpoint following it.
    /// </remarks>
    public IReadOnlyDictionary<string, ApprovalModel> ApprovalsByPrecedingStep { get; }

    /// <summary>
    /// Gets the forks indexed by their previous step name.
    /// </summary>
    /// <remarks>
    /// This lookup allows determining if a step has a fork following it.
    /// </remarks>
    public IReadOnlyDictionary<string, ForkModel> ForksByPreviousStep { get; }

    /// <summary>
    /// Gets the fork and path info indexed by the last step of each path.
    /// </summary>
    /// <remarks>
    /// This lookup allows determining if a step ends a fork path.
    /// </remarks>
    public IReadOnlyDictionary<string, (ForkModel Fork, ForkPathModel Path)> ForkPathInfo { get; }

    /// <summary>
    /// Gets the forks indexed by their join step name.
    /// </summary>
    /// <remarks>
    /// This lookup allows determining if a step is a join step for a fork.
    /// </remarks>
    public IReadOnlyDictionary<string, ForkModel> ForksByJoinStep { get; }

    /// <summary>
    /// Gets the set of all fork path step names.
    /// </summary>
    /// <remarks>
    /// This lookup allows determining if any step is part of a fork path.
    /// Fork path steps require special handling in worker command generation
    /// because they use the full prefixed step name rather than the base step type.
    /// </remarks>
    public IReadOnlyCollection<string> ForkPathSteps { get; }

    /// <summary>
    /// Gets the classification of which step-name entries lie on the workflow's main linear
    /// flow and which are reached only through their own construct.
    /// </summary>
    /// <remarks>
    /// This is the single source every successor scan consults. Carrying a private skip list
    /// instead is what let the declared terminal chain into an appended step.
    /// </remarks>
    public MainFlowClassification MainFlow { get; }

    /// <summary>
    /// Gets the step names that are not on the workflow's main linear flow.
    /// </summary>
    /// <remarks>
    /// The union of fork-path steps, branch-case steps (including the cases of a branch a loop
    /// runs on exit), failure-handler steps, approval rejection and escalation steps, and
    /// lowered low-confidence handler steps. A fork's JOIN step is NOT a member: it resumes the
    /// main flow, so <see cref="ForkPathSteps"/> — which includes the join for worker-command
    /// naming — is not usable as this set.
    /// </remarks>
    public IReadOnlyCollection<string> OffMainFlowSteps => MainFlow.OffMainFlowStepNames;

    private SagaEmissionContext(WorkflowModel model)
    {
        Model = model;
        SagaClassName = NamingHelper.GetSagaClassName(model.PascalName, model.Version);
        MainFlow = MainFlowClassification.For(model);
        LoopsByLastStep = BuildLoopsByLastStep(model);
        BranchesByPreviousStep = BuildBranchesByPreviousStep(model);
        BranchPathInfo = BuildBranchPathInfo(model);
        StepsByName = BuildStepsByName(model);
        ApprovalsByPrecedingStep = BuildApprovalsByPrecedingStep(model);
        ForksByPreviousStep = BuildForksByPreviousStep(model);
        ForkPathInfo = BuildForkPathInfo(model);
        ForksByJoinStep = BuildForksByJoinStep(model);
        ForkPathSteps = BuildForkPathSteps(model);
    }

    /// <summary>
    /// Creates a new <see cref="SagaEmissionContext"/> for the specified model.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <returns>A new context with pre-computed lookups.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    public static SagaEmissionContext Create(WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));
        return new SagaEmissionContext(model);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<LoopModel>> BuildLoopsByLastStep(WorkflowModel model)
    {
        if (!model.HasLoops)
        {
            return new Dictionary<string, IReadOnlyList<LoopModel>>();
        }

        // Group loops by their last body step, then order by nesting depth (innermost first)
        // Nesting depth is determined by the number of underscores in the full prefix
        return model.Loops!
            .GroupBy(l => l.LastBodyStepName)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LoopModel>)g
                    .OrderByDescending(l => l.FullPrefix.Count(c => c == '_'))
                    .ToList());
    }

    private static IReadOnlyDictionary<string, BranchModel> BuildBranchesByPreviousStep(WorkflowModel model)
    {
        if (!model.HasBranches)
        {
            return new Dictionary<string, BranchModel>();
        }

        // Filter out branches that follow other branches (PreviousStepName is empty)
        // These are part of a chain and will be triggered via RejoinStepName routing
        return model.Branches!
            .Where(b => !string.IsNullOrEmpty(b.PreviousStepName))
            .ToDictionary(b => b.PreviousStepName, b => b);
    }

    private static IReadOnlyDictionary<string, (BranchModel Branch, BranchCaseModel Case)> BuildBranchPathInfo(WorkflowModel model)
    {
        var result = new Dictionary<string, (BranchModel, BranchCaseModel)>();

        if (!model.HasBranches)
        {
            return result;
        }

        foreach (var branch in model.Branches!)
        {
            foreach (var branchCase in branch.Cases)
            {
                // Only include non-terminal branch paths with steps
                if (!branchCase.IsTerminal && branchCase.StepNames.Count > 0)
                {
                    result[branchCase.LastStepName] = (branch, branchCase);
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, StepModel> BuildStepsByName(WorkflowModel model)
    {
        if (model.Steps is null || model.Steps.Count == 0)
        {
            return new Dictionary<string, StepModel>();
        }

        // Use PhaseName as key to handle duplicate step types in different contexts (main flow vs loop body)
        return model.Steps.ToDictionary(s => s.PhaseName, s => s);
    }

    private static IReadOnlyDictionary<string, ApprovalModel> BuildApprovalsByPrecedingStep(WorkflowModel model)
    {
        if (!model.HasApprovalPoints)
        {
            return new Dictionary<string, ApprovalModel>();
        }

        return model.ApprovalPoints!.ToDictionary(a => a.PrecedingStepName, a => a);
    }

    private static IReadOnlyDictionary<string, ForkModel> BuildForksByPreviousStep(WorkflowModel model)
    {
        if (!model.HasForks)
        {
            return new Dictionary<string, ForkModel>();
        }

        return model.Forks!.ToDictionary(f => f.PreviousStepName, f => f);
    }

    private static IReadOnlyDictionary<string, (ForkModel Fork, ForkPathModel Path)> BuildForkPathInfo(WorkflowModel model)
    {
        var result = new Dictionary<string, (ForkModel, ForkPathModel)>();

        if (!model.HasForks)
        {
            return result;
        }

        foreach (var fork in model.Forks!)
        {
            foreach (var path in fork.Paths)
            {
                // Get the last step in this path
                if (path.StepNames.Count > 0)
                {
                    var lastStepName = path.StepNames[path.StepNames.Count - 1];
                    result[lastStepName] = (fork, path);
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ForkModel> BuildForksByJoinStep(WorkflowModel model)
    {
        if (!model.HasForks)
        {
            return new Dictionary<string, ForkModel>();
        }

        return model.Forks!.ToDictionary(f => f.JoinStepName, f => f);
    }

    private static IReadOnlyCollection<string> BuildForkPathSteps(WorkflowModel model)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (!model.HasForks)
        {
            return result;
        }

        foreach (var fork in model.Forks!)
        {
            // Add all fork path steps
            foreach (var path in fork.Paths)
            {
                foreach (var stepName in path.StepNames)
                {
                    result.Add(stepName);
                }
            }

            // Add join step (also needs full prefixed name for worker command/event)
            if (!string.IsNullOrEmpty(fork.JoinStepName))
            {
                result.Add(fork.JoinStepName);
            }
        }

        return result;
    }
}
