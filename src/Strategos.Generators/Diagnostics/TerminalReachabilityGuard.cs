// -----------------------------------------------------------------------
// <copyright file="TerminalReachabilityGuard.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;

using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Diagnostics;

/// <summary>
/// Decides, at emission time, whether a workflow's main flow actually ends at the termination
/// its author declared — and reports it when it does not.
/// </summary>
/// <remarks>
/// <para>
/// The generator holds both the declared terminal and every computed successor, so an
/// OVER-reachable terminal — one that is not last on the main flow, or a main-flow step whose
/// successor is construct-owned — is answerable from position. The complementary fault is a
/// terminal that is last but that a rejoin construct's last step does not dispatch. That
/// needs the same route graph the transition table emits (<see cref="PhaseGraph"/>), because
/// a branch whose cases all complete legitimately dispatches its declared terminal zero times.
/// Until this guard existed the only thing that caught either arm was a container-backed saga
/// run, which most contributors cannot execute; a defect the compiler can see should not need
/// Postgres to surface.
/// </para>
/// <para>
/// The off-main-flow classification arrives as a PARAMETER rather than being read from the model.
/// That seam is what makes the over-reach arm testable as a counterfactual: handed an empty
/// classification it reproduces the state in which every appended path step was a candidate
/// successor, and the diagnostic fires. The under-reach arm accepts an optional
/// <see cref="PhaseGraph"/> for the same reason: a test can strip the Finally edge a rejoin
/// last step should have published, without forking the builder the emitter uses.
/// </para>
/// </remarks>
internal static class TerminalReachabilityGuard
{
    /// <summary>
    /// Reports <c>UnreachableTermination</c> for each main-flow step whose successor, resolved
    /// under the supplied classification, is not a step the main flow may chain into.
    /// </summary>
    /// <param name="model">The workflow model whose step-name list and constructs are inspected.</param>
    /// <param name="offMainFlowStepNames">
    /// The classification the emitter will chain by: the step names that are reached through their
    /// own construct rather than by falling off the end of the preceding main-flow step.
    /// </param>
    /// <param name="declaredTerminalStepName">
    /// The step the workflow declared as its termination, or null when it declared none (which
    /// the missing-terminal diagnostic already reports).
    /// </param>
    /// <param name="location">The diagnostic location.</param>
    /// <param name="diagnostics">The accumulator to append to.</param>
    /// <param name="phaseGraph">
    /// The route graph to consult for the under-reach arm, or null to build it from
    /// <paramref name="model"/>. Tests pass a graph with a Finally edge stripped.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public static void Report(
        WorkflowModel model,
        IReadOnlyCollection<string> offMainFlowStepNames,
        string? declaredTerminalStepName,
        Location location,
        List<Diagnostic> diagnostics,
        PhaseGraph? phaseGraph = null)
    {
        ThrowHelper.ThrowIfNull(model, nameof(model));
        ThrowHelper.ThrowIfNull(offMainFlowStepNames, nameof(offMainFlowStepNames));
        ThrowHelper.ThrowIfNull(diagnostics, nameof(diagnostics));

        if (model.StepNames is null || model.StepNames.Count == 0)
        {
            return;
        }

        var chainedBy = new HashSet<string>(offMainFlowStepNames, StringComparer.Ordinal);
        var constructOwned = CollectConstructOwnedStepNames(model);

        // Reported at most once per (step, successor): the declared terminal's own check and the
        // per-step scan agree on the same pair whenever the terminal chains into a construct.
        var reported = new HashSet<string>(StringComparer.Ordinal);

        // A declared terminal with ANY successor is unreachable termination, whether or not the
        // successor belongs to a construct the classification knows about. That is the condition
        // that survives a lowering block nobody classified — the scan below cannot see one,
        // because it recognises a bad successor only by the constructs it is owned by.
        if (declaredTerminalStepName is not null)
        {
            var terminalIndex = IndexOf(model.StepNames, declaredTerminalStepName);
            if (terminalIndex >= 0 && !chainedBy.Contains(declaredTerminalStepName))
            {
                var afterTerminal = NextNotIn(model.StepNames, terminalIndex, chainedBy);
                if (afterTerminal is not null)
                {
                    Report(diagnostics, location, reported, declaredTerminalStepName, model.WorkflowName, afterTerminal);
                }
            }
        }

        for (var i = 0; i < model.StepNames.Count; i++)
        {
            var stepName = model.StepNames[i];
            if (chainedBy.Contains(stepName))
            {
                continue;
            }

            var successor = NextNotIn(model.StepNames, i, chainedBy);
            if (successor is null || !constructOwned.Contains(successor))
            {
                continue;
            }

            Report(diagnostics, location, reported, stepName, model.WorkflowName, successor);
        }

        if (declaredTerminalStepName is not null)
        {
            ReportUnderReach(
                model,
                declaredTerminalStepName,
                location,
                diagnostics,
                reported,
                phaseGraph ?? PhaseGraph.Build(model));
        }
    }

    /// <summary>
    /// Reports <c>UnreachableTermination</c> when a rejoin construct's last step does not
    /// dispatch the declared terminal.
    /// </summary>
    /// <remarks>
    /// Fired only for constructs marked rejoin — fork join, a rejoining branch or loop-exit
    /// case, an approval resume, a linear predecessor. A branch whose cases all
    /// <c>Complete()</c> plus a <c>Finally</c> legitimately dispatches the terminal zero
    /// times and stays silent. Argument 0 is the declared terminal; argument 2 is the last
    /// step that should have dispatched it.
    /// </remarks>
    private static void ReportUnderReach(
        WorkflowModel model,
        string declaredTerminalStepName,
        Location location,
        List<Diagnostic> diagnostics,
        HashSet<string> reported,
        PhaseGraph graph)
    {
        foreach (var lastStep in EnumerateRejoinDispatchersOf(model, declaredTerminalStepName))
        {
            if (graph.SuccessorsOf(lastStep).Contains(declaredTerminalStepName, StringComparer.Ordinal))
            {
                continue;
            }

            Report(
                diagnostics,
                location,
                reported,
                declaredTerminalStepName,
                model.WorkflowName,
                lastStep);
        }
    }

    /// <summary>
    /// Enumerates last steps of rejoin constructs that are supposed to dispatch the declared
    /// terminal, first-seen order.
    /// </summary>
    /// <param name="model">The workflow model to walk.</param>
    /// <param name="terminal">The declared terminal step name.</param>
    /// <returns>The last steps that should list <paramref name="terminal"/> as a successor.</returns>
    private static IEnumerable<string> EnumerateRejoinDispatchersOf(WorkflowModel model, string terminal)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        void Add(string? stepName)
        {
            if (stepName is not null && seen.Add(stepName))
            {
                ordered.Add(stepName);
            }
        }

        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                if (!string.Equals(fork.JoinStepName, terminal, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var path in fork.Paths)
                {
                    if (path.Steps.Count > 0)
                    {
                        Add(path.LastStepName);
                    }
                }
            }
        }

        AddBranchRejoinDispatchers(model.Branches, terminal, Add);

        if (model.Loops is not null)
        {
            foreach (var loop in model.Loops)
            {
                if (loop.BranchOnExit is not null)
                {
                    AddBranchRejoinDispatchers([loop.BranchOnExit], terminal, Add);
                    continue;
                }

                if (loop.BodySteps.Count > 0
                    && string.Equals(loop.ContinuationStepName, terminal, StringComparison.Ordinal))
                {
                    Add(loop.LastBodyStepName);
                }
            }
        }

        var classification = MainFlowClassification.For(model);
        AddApprovalResumeDispatchers(model.ApprovalPoints, classification, terminal, Add);
        AddConfidenceRejoinDispatchers(model, terminal, Add);
        AddLinearPredecessor(model, classification, terminal, Add);

        return ordered;
    }

    private static void AddBranchRejoinDispatchers(
        IReadOnlyList<BranchModel>? branches,
        string terminal,
        Action<string?> add)
    {
        if (branches is null)
        {
            return;
        }

        foreach (var branch in branches)
        {
            if (branch.RejoinStepName is null
                || !string.Equals(branch.RejoinStepName, terminal, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var branchCase in branch.Cases)
            {
                if (branchCase.IsTerminal || branchCase.StepNames.Count == 0)
                {
                    continue;
                }

                add(branchCase.LastStepName);
            }
        }
    }

    private static void AddApprovalResumeDispatchers(
        IReadOnlyList<ApprovalModel>? approvals,
        MainFlowClassification classification,
        string terminal,
        Action<string?> add)
    {
        if (approvals is null)
        {
            return;
        }

        foreach (var approval in approvals)
        {
            AddApprovalResumePath(
                approval.RejectionSteps,
                approval.IsRejectionTerminal,
                approval.PrecedingStepName,
                classification,
                terminal,
                add);
            AddApprovalResumePath(
                approval.EscalationSteps,
                approval.IsEscalationTerminal,
                approval.PrecedingStepName,
                classification,
                terminal,
                add);
            AddApprovalResumeDispatchers(
                approval.NestedEscalationApprovals,
                classification,
                terminal,
                add);
        }
    }

    private static void AddApprovalResumePath(
        IReadOnlyList<StepModel>? pathSteps,
        bool isTerminal,
        string precedingStepName,
        MainFlowClassification classification,
        string terminal,
        Action<string?> add)
    {
        if (isTerminal || pathSteps is null || pathSteps.Count == 0)
        {
            return;
        }

        var resume = classification.NextMainFlowStepNameAfter(precedingStepName);
        if (string.Equals(resume, terminal, StringComparison.Ordinal))
        {
            add(pathSteps[pathSteps.Count - 1].StepName);
        }
    }

    private static void AddConfidenceRejoinDispatchers(
        WorkflowModel model,
        string terminal,
        Action<string?> add)
    {
        if (model.ConfidenceHandlerStepNames is null)
        {
            return;
        }

        foreach (var stepName in model.ConfidenceHandlerStepNames)
        {
            var (_, isLastInChain, rejoinStepName) = model.GetConfidenceHandlerChainRouting(stepName);
            if (isLastInChain && string.Equals(rejoinStepName, terminal, StringComparison.Ordinal))
            {
                add(stepName);
            }
        }
    }

    private static void AddLinearPredecessor(
        WorkflowModel model,
        MainFlowClassification classification,
        string terminal,
        Action<string?> add)
    {
        var dispatchers = CollectConstructDispatchers(model);

        foreach (var stepName in model.StepNames)
        {
            if (classification.IsOffMainFlow(stepName)
                || dispatchers.Contains(stepName)
                || string.Equals(stepName, terminal, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(
                    classification.NextMainFlowStepNameAfter(stepName),
                    terminal,
                    StringComparison.Ordinal))
            {
                add(stepName);
            }
        }
    }

    /// <summary>
    /// Collects steps that dispatch into a construct rather than linearly chaining to the
    /// next main-flow step — a branch or fork predecessor, or a loop body that exits through
    /// a branch. Those steps must not be treated as linear predecessors of the terminal:
    /// an all-<c>Complete()</c> branch plus <c>Finally</c> has a predecessor whose next
    /// main-flow step is the terminal, and that shape must stay silent.
    /// </summary>
    /// <param name="model">The workflow model to walk.</param>
    /// <returns>The dispatcher step names.</returns>
    private static HashSet<string> CollectConstructDispatchers(WorkflowModel model)
    {
        var dispatchers = new HashSet<string>(StringComparer.Ordinal);

        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                dispatchers.Add(fork.PreviousStepName);
            }
        }

        if (model.Branches is not null)
        {
            foreach (var branch in model.Branches)
            {
                if (!string.IsNullOrEmpty(branch.PreviousStepName))
                {
                    dispatchers.Add(branch.PreviousStepName);
                }
            }
        }

        if (model.Loops is not null)
        {
            foreach (var loop in model.Loops)
            {
                if (loop.BranchOnExit is not null && loop.BodySteps.Count > 0)
                {
                    dispatchers.Add(loop.LastBodyStepName);
                }
            }
        }

        return dispatchers;
    }

    private static void Report(
        List<Diagnostic> diagnostics,
        Location location,
        HashSet<string> reported,
        string stepName,
        string workflowName,
        string successorStepName)
    {
        if (!reported.Add($"{stepName}{successorStepName}"))
        {
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            WorkflowDiagnostics.UnreachableTermination,
            location,
            stepName,
            workflowName,
            successorStepName));
    }

    /// <summary>
    /// Collects every step name that belongs to a construct — the steps a main flow may never
    /// chain into, because each is reached through its own construct's handler.
    /// </summary>
    /// <param name="model">The workflow model to walk.</param>
    /// <returns>The construct-owned step names.</returns>
    /// <remarks>
    /// Read straight off the model's construct lists rather than from the emitter's classification,
    /// so the guard compares what the emitter will chain by against what the workflow actually
    /// declares. Sharing one derivation would make the comparison a tautology.
    /// </remarks>
    private static HashSet<string> CollectConstructOwnedStepNames(WorkflowModel model)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);

        if (model.Forks is not null)
        {
            foreach (var fork in model.Forks)
            {
                foreach (var path in fork.Paths)
                {
                    AddAll(owned, path.StepNames);
                }
            }
        }

        if (model.Branches is not null)
        {
            foreach (var branch in model.Branches)
            {
                foreach (var branchCase in branch.Cases)
                {
                    AddAll(owned, branchCase.StepNames);
                }
            }
        }

        if (model.Loops is not null)
        {
            foreach (var loop in model.Loops)
            {
                if (loop.BranchOnExit is null)
                {
                    continue;
                }

                foreach (var branchCase in loop.BranchOnExit.Cases)
                {
                    AddAll(owned, branchCase.StepNames);
                }
            }
        }

        if (model.FailureHandlers is not null)
        {
            foreach (var handler in model.FailureHandlers)
            {
                AddAll(owned, handler.StepNames);
            }
        }

        AddApprovalSteps(model.ApprovalPoints, owned);

        if (model.ConfidenceHandlerStepNames is not null)
        {
            AddAll(owned, model.ConfidenceHandlerStepNames);
        }

        return owned;
    }

    private static void AddApprovalSteps(IReadOnlyList<ApprovalModel>? approvals, HashSet<string> owned)
    {
        if (approvals is null)
        {
            return;
        }

        foreach (var approval in approvals)
        {
            if (approval.RejectionSteps is not null)
            {
                foreach (var step in approval.RejectionSteps)
                {
                    owned.Add(step.StepName);
                }
            }

            if (approval.EscalationSteps is not null)
            {
                foreach (var step in approval.EscalationSteps)
                {
                    owned.Add(step.StepName);
                }
            }

            AddApprovalSteps(approval.NestedEscalationApprovals, owned);
        }
    }

    private static void AddAll(HashSet<string> owned, IReadOnlyList<string>? stepNames)
    {
        if (stepNames is null)
        {
            return;
        }

        foreach (var stepName in stepNames)
        {
            owned.Add(stepName);
        }
    }

    private static void AddAll(HashSet<string> owned, IReadOnlyCollection<string>? stepNames)
    {
        if (stepNames is null)
        {
            return;
        }

        foreach (var stepName in stepNames)
        {
            owned.Add(stepName);
        }
    }

    private static string? NextNotIn(IReadOnlyList<string> stepNames, int index, HashSet<string> skip)
    {
        for (var j = index + 1; j < stepNames.Count; j++)
        {
            if (!skip.Contains(stepNames[j]))
            {
                return stepNames[j];
            }
        }

        return null;
    }

    private static int IndexOf(IReadOnlyList<string> stepNames, string stepName)
    {
        for (var i = 0; i < stepNames.Count; i++)
        {
            if (string.Equals(stepNames[i], stepName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
