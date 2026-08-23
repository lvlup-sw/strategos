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
/// successor is construct-owned — is answerable before anything runs. The complementary fault,
/// a terminal that is last but that nothing dispatches, is NOT decided here: it needs route
/// analysis rather than position, because a branch whose cases all complete legitimately
/// dispatches its declared terminal zero times. Until this guard
/// existed the only thing that caught it was a container-backed saga run, which most contributors
/// cannot execute; a defect the compiler can see should not need Postgres to surface.
/// </para>
/// <para>
/// The off-main-flow classification arrives as a PARAMETER rather than being read from the model.
/// That seam is what makes the guard testable as a counterfactual: handed an empty classification
/// it reproduces the state in which every appended path step was a candidate successor, and the
/// diagnostic fires — which is the evidence that it would have caught the shipped defect rather
/// than a claim that it would.
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
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public static void Report(
        WorkflowModel model,
        IReadOnlyCollection<string> offMainFlowStepNames,
        string? declaredTerminalStepName,
        Location location,
        List<Diagnostic> diagnostics)
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
