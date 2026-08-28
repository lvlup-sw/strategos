// -----------------------------------------------------------------------
// <copyright file="PhaseGraph.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Models;

/// <summary>
/// Resolves each step phase's real successors from the workflow's constructs.
/// </summary>
/// <remarks>
/// <para>
/// Shared by the transition table emitter and the termination-reachability guard so the
/// diagnostic and the emitted <c>ValidTransitions</c> table cannot drift. Successors come
/// from two kinds of edge. A <em>routed</em> edge is stated by the construct the step
/// belongs to — a fork dispatching its paths, a path's last step reaching the join, a
/// branch dispatching its cases, a case's last step rejoining or ending the workflow, a
/// loop body's last step continuing or exiting, a handler chain advancing. A routed edge
/// replaces linear chaining: a step that has one never also falls through to the next
/// list entry.
/// </para>
/// <para>
/// An <em>additional</em> edge coexists with linear chaining. A confidence-gated step still
/// proceeds along the main flow when the score clears the threshold, so its edge to the
/// low-confidence handler is recorded alongside, not instead of, its main-flow successor.
/// </para>
/// <para>
/// Every target is either an entry of the workflow's step-name list or one of the two
/// standard terminals, so a target is always a member of the emitted phase enum without this
/// type having to restate the enum emitter's membership rules.
/// </para>
/// </remarks>
internal sealed class PhaseGraph
{
    /// <summary>
    /// The completed-workflow phase name.
    /// </summary>
    internal const string CompletedPhase = "Completed";

    /// <summary>
    /// The failed-workflow phase name.
    /// </summary>
    internal const string FailedPhase = "Failed";

    private readonly Dictionary<string, List<string>> _successors;

    private PhaseGraph(Dictionary<string, List<string>> successors, string entryPhaseName)
    {
        _successors = successors;
        EntryPhaseName = entryPhaseName;
    }

    /// <summary>
    /// Gets the phase the workflow enters from <c>NotStarted</c>.
    /// </summary>
    public string EntryPhaseName { get; }

    /// <summary>
    /// Builds the phase graph for a workflow model.
    /// </summary>
    /// <param name="model">The workflow model to resolve.</param>
    /// <returns>The resolved graph.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    public static PhaseGraph Build(WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var classification = MainFlowClassification.For(model);
        var successors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var routed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stepName in model.StepNames)
        {
            successors[stepName] = [];
        }

        var builder = new EdgeBuilder(model, successors, routed);

        builder.AddForkEdges();
        builder.AddBranchEdges();
        builder.AddLoopEdges();
        builder.AddFailureHandlerEdges(classification);
        builder.AddApprovalPathEdges(classification);
        builder.AddConfidenceHandlerChainEdges();
        builder.AddMainFlowEdges(classification);
        builder.AddConfidenceGateEdges();
        builder.AddFailedEdges();

        var entryPhaseName = classification.NextMainFlowStepNameAfterIndex(-1) ?? CompletedPhase;

        return new PhaseGraph(successors, entryPhaseName);
    }

    /// <summary>
    /// Gets the successor phases of a step phase.
    /// </summary>
    /// <param name="stepName">The step phase name.</param>
    /// <returns>The ordered successor phase names.</returns>
    public IReadOnlyList<string> SuccessorsOf(string stepName) =>
        _successors.TryGetValue(stepName, out var targets) ? targets : [];

    /// <summary>
    /// Returns a graph identical to this one except the named successor is removed from the
    /// named step.
    /// </summary>
    /// <param name="stepName">The step whose successor list is edited.</param>
    /// <param name="target">The successor to drop.</param>
    /// <returns>A new graph with that one edge removed.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="stepName"/> or <paramref name="target"/> is null.
    /// </exception>
    /// <remarks>
    /// The diagnostic and the emitter share <see cref="Build"/>. Tests use this to simulate
    /// the next dropped rejoin edge without forking the builder — the same seam the
    /// over-reach arm uses when it injects a counterfactual classification.
    /// </remarks>
    public PhaseGraph WithoutSuccessor(string stepName, string target)
    {
        ThrowHelper.ThrowIfNull(stepName, nameof(stepName));
        ThrowHelper.ThrowIfNull(target, nameof(target));

        var copy = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var pair in _successors)
        {
            copy[pair.Key] = [.. pair.Value];
        }

        if (copy.TryGetValue(stepName, out var targets))
        {
            targets.RemoveAll(t => string.Equals(t, target, StringComparison.Ordinal));
        }

        return new PhaseGraph(copy, EntryPhaseName);
    }

    /// <summary>
    /// Accumulates the edges of a <see cref="PhaseGraph"/>, one construct at a time.
    /// </summary>
    private sealed class EdgeBuilder(
        WorkflowModel model,
        Dictionary<string, List<string>> successors,
        HashSet<string> routed)
    {
        /// <summary>
        /// Adds the fork dispatch, in-path and join edges of every fork.
        /// </summary>
        public void AddForkEdges()
        {
            if (model.Forks is null)
            {
                return;
            }

            foreach (var fork in model.Forks)
            {
                // The fork's predecessor dispatches every path in parallel. Chaining it to
                // one path's first step publishes a sequence the workflow never runs.
                foreach (var path in fork.Paths)
                {
                    if (path.Steps.Count == 0)
                    {
                        continue;
                    }

                    AddRouted(fork.PreviousStepName, path.FirstStepName);
                    AddPathInterior(path.StepNames);

                    // A path's last step reaches the join, not the next sibling path.
                    AddRouted(path.LastStepName, fork.JoinStepName);
                }
            }
        }

        /// <summary>
        /// Adds the case dispatch, in-case and rejoin/termination edges of every branch the
        /// workflow declares.
        /// </summary>
        public void AddBranchEdges()
        {
            if (model.Branches is null)
            {
                return;
            }

            foreach (var branch in model.Branches)
            {
                AddBranch(branch, branch.PreviousStepName);
            }
        }

        /// <summary>
        /// Adds the continue and exit edges of every loop's last body step, and the cases of a
        /// branch the loop evaluates on exit.
        /// </summary>
        /// <remarks>
        /// A loop-exit branch carries no predecessor of its own — the loop, not a step, is what
        /// precedes it — so the loop's last body step is what dispatches its cases.
        /// </remarks>
        public void AddLoopEdges()
        {
            if (model.Loops is null)
            {
                return;
            }

            foreach (var loop in model.Loops)
            {
                if (loop.BodySteps.Count == 0)
                {
                    continue;
                }

                // Continue: the body's last step re-enters the body's first step.
                AddRouted(loop.LastBodyStepName, loop.FirstBodyStepName);

                // Exit: either straight to the continuation step, or through the exit branch.
                if (loop.BranchOnExit is null)
                {
                    AddRouted(loop.LastBodyStepName, loop.ContinuationStepName ?? CompletedPhase);
                }
                else
                {
                    AddBranch(loop.BranchOnExit, loop.LastBodyStepName);
                }
            }
        }

        /// <summary>
        /// Adds the chain edges of every failure handler, and the resume edge of a handler that
        /// recovers rather than terminating.
        /// </summary>
        /// <param name="classification">The workflow's main-flow classification.</param>
        public void AddFailureHandlerEdges(MainFlowClassification classification)
        {
            if (model.FailureHandlers is null)
            {
                return;
            }

            foreach (var handler in model.FailureHandlers)
            {
                if (handler.StepNames.Count == 0)
                {
                    continue;
                }

                AddPathInterior(handler.StepNames);

                if (handler.IsTerminal)
                {
                    // A terminal handler ends the workflow in Failed, which every step already
                    // reaches; no forward edge is claimed for the chain's last step.
                    MarkRouted(handler.LastStepName);
                    continue;
                }

                var resumeStepName = handler.TriggerStepName is null
                    ? null
                    : classification.NextMainFlowStepNameAfter(handler.TriggerStepName);

                AddRouted(handler.LastStepName, resumeStepName ?? CompletedPhase);
            }
        }

        /// <summary>
        /// Adds the chain edges of every approval point's rejection and escalation paths,
        /// including those of nested escalation approvals.
        /// </summary>
        /// <param name="classification">The workflow's main-flow classification.</param>
        public void AddApprovalPathEdges(MainFlowClassification classification) =>
            AddApprovalPathEdges(model.ApprovalPoints, classification);

        /// <summary>
        /// Adds the chain edges of every lowered low-confidence handler step: the next step of
        /// its own chain, or where the chain's last step rejoins or ends the workflow.
        /// </summary>
        public void AddConfidenceHandlerChainEdges()
        {
            if (model.ConfidenceHandlerStepNames is null)
            {
                return;
            }

            foreach (var stepName in model.ConfidenceHandlerStepNames)
            {
                var (nextHandlerStepName, isLastInChain, rejoinStepName) =
                    model.GetConfidenceHandlerChainRouting(stepName);

                if (nextHandlerStepName is not null)
                {
                    AddRouted(stepName, nextHandlerStepName);
                    continue;
                }

                if (isLastInChain)
                {
                    AddRouted(stepName, rejoinStepName ?? CompletedPhase);
                }
            }
        }

        /// <summary>
        /// Chains every step that no construct routed to its next main-flow step, or to
        /// completion when it is the main flow's last step.
        /// </summary>
        /// <param name="classification">The workflow's main-flow classification.</param>
        public void AddMainFlowEdges(MainFlowClassification classification)
        {
            for (var i = 0; i < model.StepNames.Count; i++)
            {
                var stepName = model.StepNames[i];

                if (routed.Contains(stepName) || classification.IsOffMainFlow(stepName))
                {
                    continue;
                }

                Add(stepName, classification.NextMainFlowStepNameAfterIndex(i) ?? CompletedPhase);
            }
        }

        /// <summary>
        /// Adds the failure edge every step carries, last so it reads as the fallback it is.
        /// </summary>
        public void AddFailedEdges()
        {
            foreach (var stepName in model.StepNames)
            {
                Add(stepName, FailedPhase);
            }
        }

        private void AddApprovalPathEdges(
            IReadOnlyList<ApprovalModel>? approvals,
            MainFlowClassification classification)
        {
            if (approvals is null)
            {
                return;
            }

            foreach (var approval in approvals)
            {
                AddApprovalPath(approval.RejectionSteps, approval.IsRejectionTerminal, approval, classification);
                AddApprovalPath(approval.EscalationSteps, approval.IsEscalationTerminal, approval, classification);
                AddApprovalPathEdges(approval.NestedEscalationApprovals, classification);
            }
        }

        private void AddApprovalPath(
            IReadOnlyList<StepModel>? pathSteps,
            bool isTerminal,
            ApprovalModel approval,
            MainFlowClassification classification)
        {
            if (pathSteps is null || pathSteps.Count == 0)
            {
                return;
            }

            var stepNames = pathSteps.Select(s => s.StepName).ToList();
            AddPathInterior(stepNames);

            var lastStepName = stepNames[stepNames.Count - 1];

            if (isTerminal)
            {
                // A rejection or escalation path that declared its own completion ends the
                // workflow in Completed, not Failed. That is what separates it from a terminal
                // failure handler, which ends in Failed — an edge every step already carries, so
                // that one claims no forward edge. Omitting the edge here published a last step
                // whose only successor was Failed while the saga set the completed phase, so the
                // table forbade the transition the generated saga performs.
                AddRouted(lastStepName, CompletedPhase);
                return;
            }

            AddRouted(
                lastStepName,
                classification.NextMainFlowStepNameAfter(approval.PrecedingStepName) ?? CompletedPhase);
        }

        /// <summary>
        /// Adds the gate edge from each confidence-gated step to its low-confidence handler
        /// chain's first step.
        /// </summary>
        /// <remarks>
        /// Added after the main-flow pass and never marked routed: a score above the threshold
        /// still advances the step along whatever edge its own construct gives it, so the gate
        /// edge coexists with that successor instead of replacing it.
        /// </remarks>
        public void AddConfidenceGateEdges()
        {
            foreach (var step in EnumerateGatedSteps())
            {
                var chain = step.Confidence?.OnLowConfidenceHandlerChain;
                if (chain is null || chain.Steps.Count == 0)
                {
                    continue;
                }

                Add(step.PhaseName, chain.Steps[0].StepName);
            }
        }

        private IEnumerable<StepModel> EnumerateGatedSteps()
        {
            if (model.Steps is not null)
            {
                foreach (var step in model.Steps)
                {
                    yield return step;
                }
            }

            if (model.Forks is null)
            {
                yield break;
            }

            foreach (var fork in model.Forks)
            {
                foreach (var path in fork.Paths)
                {
                    foreach (var step in path.Steps)
                    {
                        yield return step;
                    }
                }
            }
        }

        private void AddBranch(BranchModel branch, string dispatchingStepName)
        {
            // A branch reached from a preceding branch's fall-through carries no predecessor of
            // its own, so it contributes cases without a dispatch edge.
            var hasDispatcher = !string.IsNullOrEmpty(dispatchingStepName);

            foreach (var branchCase in branch.Cases)
            {
                if (branchCase.StepNames.Count == 0)
                {
                    continue;
                }

                if (hasDispatcher)
                {
                    AddRouted(dispatchingStepName, branchCase.FirstStepName);
                }

                AddPathInterior(branchCase.StepNames);

                // A case that ends the workflow completes; otherwise it converges on the
                // rejoin step. Either way it never chains to the next sibling case.
                var target = branchCase.IsTerminal || branch.RejoinStepName is null
                    ? CompletedPhase
                    : branch.RejoinStepName;

                AddRouted(branchCase.LastStepName, target);
            }
        }

        private void AddPathInterior(IReadOnlyList<string> pathStepNames)
        {
            for (var i = 0; i < pathStepNames.Count - 1; i++)
            {
                AddRouted(pathStepNames[i], pathStepNames[i + 1]);
            }
        }

        private void AddRouted(string stepName, string target)
        {
            MarkRouted(stepName);
            Add(stepName, target);
        }

        private void MarkRouted(string stepName)
        {
            if (successors.ContainsKey(stepName))
            {
                _ = routed.Add(stepName);
            }
        }

        private void Add(string stepName, string target)
        {
            if (!successors.TryGetValue(stepName, out var targets))
            {
                return;
            }

            if (!targets.Contains(target, StringComparer.Ordinal))
            {
                targets.Add(target);
            }
        }
    }
}
