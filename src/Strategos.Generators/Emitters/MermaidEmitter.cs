// -----------------------------------------------------------------------
// <copyright file="MermaidEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters;

/// <summary>
/// Emits Mermaid state diagram source for a workflow.
/// </summary>
/// <remarks>
/// <para>
/// The step-name lookups are many-valued. Two constructs can legitimately share a boundary step
/// — a loop whose body ends where a nested loop ends is the ordinary case — and a single-valued
/// lookup turns that shape into a duplicate-key throw, which fails the whole generator rather
/// than producing a wrong picture.
/// </para>
/// <para>
/// A step that a construct routes is never also chained to the next step-name entry. The
/// step-name list carries appended off-main-flow steps, so falling through to list order draws
/// edges between exclusive paths and out of the workflow's terminal step.
/// </para>
/// </remarks>
internal static class MermaidEmitter
{
    /// <summary>
    /// Generates the Mermaid state diagram source for the given workflow model.
    /// </summary>
    /// <param name="model">The workflow model containing workflow structure information.</param>
    /// <returns>The generated Mermaid diagram source code.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    public static string Emit(WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var sb = new StringBuilder();
        var classification = MainFlowClassification.For(model);

        // Workflow name comment
        sb.AppendLine($"%% Workflow: {model.WorkflowName}");

        // Mermaid state diagram header
        sb.AppendLine("stateDiagram-v2");

        // Start transition to the first MAIN-FLOW step
        var entryStepName = classification.NextMainFlowStepNameAfterIndex(-1);
        if (entryStepName is not null)
        {
            sb.AppendLine($"    [*] --> {entryStepName}");
        }

        var loopsByFirstBodyStep = GroupBy(model.Loops, l => l.FirstBodyStepName);
        var loopsByLastBodyStep = GroupBy(model.Loops, l => l.LastBodyStepName);

        // A branch is dispatched by the step that precedes it. One that follows another branch
        // carries no predecessor of its own; one a loop evaluates on exit is dispatched by the
        // loop's last body step, because the loop rather than a step is what precedes it.
        var allBranches = EnumerateDispatchedBranches(model).ToList();
        var branchesByDispatchingStep = GroupBy(allBranches, entry => entry.DispatchingStepName);

        var branchCasesByLastStep = GroupBy(
            allBranches.SelectMany(entry => entry.Branch.Cases.Select(c => (entry.Branch, Case: c))),
            entry => entry.Case.LastStepName);

        var forksByPreviousStep = GroupBy(model.Forks, f => f.PreviousStepName);
        var forkPathEnds = GroupBy(
            model.Forks?.SelectMany(f => f.Paths.Where(p => p.Steps.Count > 0).Select(p => (Fork: f, Path: p))),
            entry => entry.Path.LastStepName);

        var routedSteps = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < model.StepNames.Count; i++)
        {
            var stepName = model.StepNames[i];
            var routed = false;

            // Check if this step has validation
            var stepModel = model.Steps?.FirstOrDefault(s => s.StepName == stepName || s.PhaseName == stepName);
            var hasValidation = stepModel?.HasValidation ?? false;

            // Validation guard failure transition (before normal transition)
            if (hasValidation)
            {
                sb.AppendLine($"    {stepName} --> ValidationFailed : guard failed");
            }

            // A step that opens one or more loop bodies is annotated once per loop.
            foreach (var loopAtFirst in Lookup(loopsByFirstBodyStep, stepName))
            {
                sb.AppendLine($"    note right of {stepName} : Loop: {loopAtFirst.LoopName} (max {loopAtFirst.MaxIterations})");
            }

            // Branch point: route through a choice state, one arm per case.
            foreach (var (branch, _) in Lookup(branchesByDispatchingStep, stepName))
            {
                var choiceName = $"BranchBy{branch.DiscriminatorPropertyPath}";
                sb.AppendLine($"    {stepName} --> {choiceName}");
                sb.AppendLine($"    state {choiceName} <<choice>>");
                foreach (var branchCase in branch.Cases)
                {
                    sb.AppendLine($"    {choiceName} --> {branchCase.FirstStepName} : {branchCase.BranchPathPrefix}");
                }

                routed = true;
            }

            // Fork point: route through a fork state, one arm per parallel path, and declare the
            // join state the paths converge on.
            foreach (var fork in Lookup(forksByPreviousStep, stepName))
            {
                var forkStateName = ForkStateName(fork);
                var joinStateName = JoinStateName(fork);

                sb.AppendLine($"    {stepName} --> {forkStateName}");
                sb.AppendLine($"    state {forkStateName} <<fork>>");
                foreach (var path in fork.Paths.Where(p => p.Steps.Count > 0))
                {
                    sb.AppendLine($"    {forkStateName} --> {path.FirstStepName}");
                }

                sb.AppendLine($"    state {joinStateName} <<join>>");
                sb.AppendLine($"    {joinStateName} --> {fork.JoinStepName}");

                routed = true;
            }

            // Interior of a parallel path or a branch case: the next step of that same path.
            if (classification.TryGetSuccessorWithinPath(stepName, out var pathSuccessor))
            {
                sb.AppendLine($"    {stepName} --> {pathSuccessor}");
                routed = true;
            }

            // End of a parallel path: converge on the join state, not on a sibling path.
            foreach (var (fork, _) in Lookup(forkPathEnds, stepName))
            {
                sb.AppendLine($"    {stepName} --> {JoinStateName(fork)}");
                routed = true;
            }

            // End of a branch case: complete, or converge on the rejoin step.
            foreach (var (parentBranch, branchCase) in Lookup(branchCasesByLastStep, stepName))
            {
                if (branchCase.IsTerminal)
                {
                    sb.AppendLine($"    {stepName} --> [*]");
                    routed = true;
                }
                else if (parentBranch.RejoinStepName is not null)
                {
                    sb.AppendLine($"    {stepName} --> {parentBranch.RejoinStepName}");
                    routed = true;
                }
            }

            // End of a loop body: continue back to the body's first step, or exit.
            foreach (var loopAtLast in Lookup(loopsByLastBodyStep, stepName))
            {
                sb.AppendLine($"    {stepName} --> {loopAtLast.FirstBodyStepName} : continue");

                if (loopAtLast.BranchOnExit is not null)
                {
                    // The exit runs a branch, whose choice state is emitted with the branch.
                }
                else if (loopAtLast.ContinuationStepName is not null)
                {
                    sb.AppendLine($"    {stepName} --> {loopAtLast.ContinuationStepName} : exit");
                }
                else
                {
                    // Terminal loop - exit to completion
                    sb.AppendLine($"    {stepName} --> [*] : exit");
                }

                routed = true;
            }

            // Main flow: only for a step no construct routed and that is on the main flow.
            if (!routed && !classification.IsOffMainFlow(stepName))
            {
                var nextMainFlowStepName = classification.NextMainFlowStepNameAfterIndex(i);
                if (nextMainFlowStepName is not null)
                {
                    sb.AppendLine($"    {stepName} --> {nextMainFlowStepName}");
                }
            }

            if (routed)
            {
                _ = routedSteps.Add(stepName);
            }

            // Every step can transition to Failed
            sb.AppendLine($"    {stepName} --> Failed");
        }

        // Completion transition from the main flow's last step, unless a construct already said
        // where that step goes.
        var exitStepName = LastMainFlowStepName(model, classification);
        if (exitStepName is not null && !routedSteps.Contains(exitStepName))
        {
            sb.AppendLine($"    {exitStepName} --> [*]");
        }

        // Failed state
        sb.AppendLine("    state Failed");

        // ValidationFailed state (only when workflow has validation guards)
        if (model.HasAnyValidation)
        {
            sb.AppendLine("    state ValidationFailed");
        }

        return sb.ToString();
    }

    private static string ForkStateName(ForkModel fork) => $"Fork_{Sanitize(fork.ForkId)}";

    private static string JoinStateName(ForkModel fork) => $"Join_{Sanitize(fork.ForkId)}";

    private static string Sanitize(string identifier) => identifier.Replace("-", "_");

    private static string? LastMainFlowStepName(WorkflowModel model, MainFlowClassification classification)
    {
        for (var i = model.StepNames.Count - 1; i >= 0; i--)
        {
            if (!classification.IsOffMainFlow(model.StepNames[i]))
            {
                return model.StepNames[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Enumerates every branch that some step dispatches, paired with the step that dispatches it.
    /// </summary>
    /// <param name="model">The workflow model to walk.</param>
    /// <returns>Each dispatchable branch and its dispatching step name.</returns>
    private static IEnumerable<(BranchModel Branch, string DispatchingStepName)> EnumerateDispatchedBranches(
        WorkflowModel model)
    {
        if (model.Branches is not null)
        {
            foreach (var branch in model.Branches)
            {
                if (!string.IsNullOrEmpty(branch.PreviousStepName))
                {
                    yield return (branch, branch.PreviousStepName);
                }
            }
        }

        if (model.Loops is null)
        {
            yield break;
        }

        foreach (var loop in model.Loops)
        {
            if (loop.BranchOnExit is not null && loop.BodySteps.Count > 0)
            {
                yield return (loop.BranchOnExit, loop.LastBodyStepName);
            }
        }
    }

    private static Dictionary<string, List<TValue>> GroupBy<TValue>(
        IEnumerable<TValue>? values,
        Func<TValue, string> keySelector)
    {
        var lookup = new Dictionary<string, List<TValue>>(StringComparer.Ordinal);

        if (values is null)
        {
            return lookup;
        }

        foreach (var value in values)
        {
            var key = keySelector(value);
            if (!lookup.TryGetValue(key, out var bucket))
            {
                bucket = [];
                lookup[key] = bucket;
            }

            bucket.Add(value);
        }

        return lookup;
    }

    private static IReadOnlyList<TValue> Lookup<TValue>(
        Dictionary<string, List<TValue>> lookup,
        string key) =>
        lookup.TryGetValue(key, out var bucket) ? bucket : [];
}
