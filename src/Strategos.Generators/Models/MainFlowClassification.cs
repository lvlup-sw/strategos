// -----------------------------------------------------------------------
// <copyright file="MainFlowClassification.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Strategos.Generators.Models;

/// <summary>
/// Classifies each entry of a workflow's step-name list as either on the workflow's main
/// linear flow or off it, and resolves main-flow adjacency from that classification rather
/// than from list position.
/// </summary>
/// <remarks>
/// <para>
/// A workflow's step-name list is not purely the main flow. Several lowering blocks append
/// names to it so that those steps get a phase, a worker handler, start/completed commands
/// and events — but the appended steps are reached through their own construct's handler,
/// never by falling off the end of the preceding main-flow step. Treating every later entry
/// as a candidate successor is what makes a declared terminal chain into a fork path or a
/// branch case instead of completing the saga.
/// </para>
/// <para>
/// The off-main-flow set is derived here, once, from every construct that contributes such a
/// name. Consumers must consult this classification rather than carrying their own skip list:
/// a partial copy is how a newly added contributing construct silently re-opens the defect.
/// </para>
/// <para>
/// The classification governs main-flow chaining only. A step that sits <em>inside</em> a
/// fork path or a branch case still has a successor — the next step of its own path — and
/// <see cref="TryGetSuccessorWithinPath"/> supplies it. Only a path's last step is
/// intercepted by a dedicated path-end handler, so without an in-path successor an
/// intermediate path step would fall through to the generic completed handler and take a
/// main-flow successor, or none at all and wrongly complete the saga mid-path.
/// </para>
/// </remarks>
internal sealed class MainFlowClassification
{
    private readonly WorkflowModel _model;
    private readonly HashSet<string> _offMainFlowStepNames;
    private readonly Dictionary<string, string> _successorWithinPath;
    private readonly Dictionary<string, ApprovalPathEnd> _approvalPathEnds;

    private MainFlowClassification(WorkflowModel model)
    {
        _model = model;
        _offMainFlowStepNames = new HashSet<string>(StringComparer.Ordinal);
        _successorWithinPath = new Dictionary<string, string>(StringComparer.Ordinal);
        _approvalPathEnds = new Dictionary<string, ApprovalPathEnd>(StringComparer.Ordinal);

        ClassifyForkPaths(model, _offMainFlowStepNames, _successorWithinPath);
        ClassifyBranchCases(model, _offMainFlowStepNames, _successorWithinPath);
        ClassifyLoopExitBranchCases(model, _offMainFlowStepNames, _successorWithinPath);
        ClassifyFailureHandlerSteps(model, _offMainFlowStepNames);
        ClassifyApprovalSteps(model.ApprovalPoints, _offMainFlowStepNames, _successorWithinPath, _approvalPathEnds);
        ClassifyConfidenceHandlerSteps(model, _offMainFlowStepNames);
    }

    /// <summary>
    /// Gets the step names that are not on the workflow's main linear flow.
    /// </summary>
    public IReadOnlyCollection<string> OffMainFlowStepNames => _offMainFlowStepNames;

    /// <summary>
    /// Creates a classification for the specified workflow model.
    /// </summary>
    /// <param name="model">The workflow model to classify.</param>
    /// <returns>The classification for <paramref name="model"/>.</returns>
    public static MainFlowClassification For(WorkflowModel model) => new(model);

    /// <summary>
    /// Determines whether the named step is off the workflow's main linear flow.
    /// </summary>
    /// <param name="stepName">The step phase name to test.</param>
    /// <returns>
    /// <see langword="true"/> when the step is reached through its own construct rather than
    /// by main-flow chaining; otherwise <see langword="false"/>.
    /// </returns>
    public bool IsOffMainFlow(string stepName) => _offMainFlowStepNames.Contains(stepName);

    /// <summary>
    /// Gets the successor of a step that sits inside a fork path or a branch case — the next
    /// step of that same path.
    /// </summary>
    /// <param name="stepName">The step phase name to resolve.</param>
    /// <param name="successorStepName">The next step in the same path, when one exists.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="stepName"/> is a non-last step of a fork
    /// path or branch case; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetSuccessorWithinPath(string stepName, out string successorStepName) =>
        _successorWithinPath.TryGetValue(stepName, out successorStepName!);

    /// <summary>
    /// Gets where an approval's rejection or escalation chain goes once its last step completes.
    /// </summary>
    /// <param name="stepName">The step phase name to resolve.</param>
    /// <param name="successorStepName">
    /// The main-flow step the chain resumes on, or null when the chain ends the workflow — either
    /// because it declared its own completion or because the approval has no main-flow successor.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="stepName"/> is the last step of a rejection or
    /// escalation chain; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Resolved on demand rather than during construction: the resume target is the next MAIN-FLOW
    /// step after the approval's preceding step, and that is only answerable once every construct
    /// has contributed to the off-main-flow set.
    /// </remarks>
    public bool TryGetApprovalPathEndSuccessor(string stepName, out string? successorStepName)
    {
        if (!_approvalPathEnds.TryGetValue(stepName, out var pathEnd))
        {
            successorStepName = null;
            return false;
        }

        successorStepName = pathEnd.EndsWorkflow
            ? null
            : NextMainFlowStepNameAfter(pathEnd.ApprovalPrecedingStepName);

        return true;
    }

    /// <summary>
    /// Gets the next main-flow step after the entry at the specified index of the workflow's
    /// step-name list, skipping every off-main-flow entry.
    /// </summary>
    /// <param name="index">The zero-based index to search after.</param>
    /// <returns>The next main-flow step phase name, or null when no later main-flow step exists.</returns>
    public string? NextMainFlowStepNameAfterIndex(int index)
    {
        for (var j = index + 1; j < _model.StepNames.Count; j++)
        {
            if (!IsOffMainFlow(_model.StepNames[j]))
            {
                return _model.StepNames[j];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the next main-flow step after the named step, skipping every off-main-flow entry.
    /// </summary>
    /// <param name="stepName">The step phase name to search after.</param>
    /// <returns>
    /// The next main-flow step phase name, or null when no later main-flow step exists or the
    /// named step is not in the workflow's step-name list.
    /// </returns>
    public string? NextMainFlowStepNameAfter(string stepName)
    {
        var index = IndexOf(stepName);
        return index < 0 ? null : NextMainFlowStepNameAfterIndex(index);
    }

    /// <summary>
    /// Gets the index of a step phase name within the workflow's step-name list.
    /// </summary>
    /// <param name="stepName">The step phase name to locate.</param>
    /// <returns>The zero-based index, or -1 when the name is absent.</returns>
    public int IndexOf(string stepName)
    {
        for (var i = 0; i < _model.StepNames.Count; i++)
        {
            if (string.Equals(_model.StepNames[i], stepName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Classifies every fork path's steps.
    /// </summary>
    /// <remarks>
    /// The fork's JOIN step is deliberately left on the main flow. It is the step that resumes
    /// the workflow once every path has completed, so its handler is precisely the one that must
    /// chain to the terminal. Reusing a set that includes the join for worker-command naming
    /// would classify it off-main-flow and strand the terminal.
    /// </remarks>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    /// <param name="successorWithinPath">The in-path successor map being accumulated.</param>
    private static void ClassifyForkPaths(
        WorkflowModel model,
        HashSet<string> offMainFlow,
        Dictionary<string, string> successorWithinPath)
    {
        if (model.Forks is null)
        {
            return;
        }

        foreach (var fork in model.Forks)
        {
            foreach (var path in fork.Paths)
            {
                ClassifyPath(path.StepNames, offMainFlow, successorWithinPath);
            }
        }
    }

    /// <summary>
    /// Classifies the steps of every case of every branch declared on the workflow.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    /// <param name="successorWithinPath">The in-path successor map being accumulated.</param>
    private static void ClassifyBranchCases(
        WorkflowModel model,
        HashSet<string> offMainFlow,
        Dictionary<string, string> successorWithinPath)
    {
        if (model.Branches is null)
        {
            return;
        }

        foreach (var branch in model.Branches)
        {
            foreach (var branchCase in branch.Cases)
            {
                ClassifyPath(branchCase.StepNames, offMainFlow, successorWithinPath);
            }
        }
    }

    /// <summary>
    /// Classifies the steps of every case of a branch that a loop runs on exit.
    /// </summary>
    /// <remarks>
    /// A branch that follows a repeat-until loop is attached to the loop, not to the workflow's
    /// branch collection — the branch extractor deliberately declines it there. A branch-path
    /// set derived only from the workflow's branches therefore misses this contributing
    /// construct entirely, which is the trap this method exists to close.
    /// </remarks>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    /// <param name="successorWithinPath">The in-path successor map being accumulated.</param>
    private static void ClassifyLoopExitBranchCases(
        WorkflowModel model,
        HashSet<string> offMainFlow,
        Dictionary<string, string> successorWithinPath)
    {
        if (model.Loops is null)
        {
            return;
        }

        foreach (var loop in model.Loops)
        {
            if (loop.BranchOnExit is null)
            {
                continue;
            }

            foreach (var branchCase in loop.BranchOnExit.Cases)
            {
                ClassifyPath(branchCase.StepNames, offMainFlow, successorWithinPath);
            }
        }
    }

    /// <summary>
    /// Classifies every failure-handler step.
    /// </summary>
    /// <remarks>
    /// Failure-handler steps carry no in-path successor here: their chaining is owned by the
    /// dedicated failure-handler component, which emits its own start and completed handlers
    /// over its own command and event types.
    /// </remarks>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    private static void ClassifyFailureHandlerSteps(WorkflowModel model, HashSet<string> offMainFlow)
    {
        if (model.FailureHandlers is null)
        {
            return;
        }

        foreach (var handler in model.FailureHandlers)
        {
            foreach (var stepName in handler.StepNames)
            {
                offMainFlow.Add(stepName);
            }
        }
    }

    /// <summary>
    /// Classifies every approval rejection and escalation step, including those of nested
    /// escalation approvals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A rejection or escalation chain is a path, so it carries in-path successors exactly as a
    /// fork path or a branch case does. The approval component dispatches only the chain's FIRST
    /// step, and it does so through the generic start command, which means the generic completed
    /// handler is the live handler for every step of the chain — the one place a chain of more
    /// than one step can advance. Without an in-path successor each step marks the saga completed,
    /// so the chain truncates after its first step and the rest never run.
    /// </para>
    /// <para>
    /// This is where approval chains differ from failure handlers, which are classified without
    /// in-path successors just above. A failure handler mints its own command and event types and
    /// chains them in its own component emitter, so the generic handlers over its step names are
    /// inert and their adjacency is unobservable.
    /// </para>
    /// </remarks>
    /// <param name="approvals">The approval models to walk.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    /// <param name="successorWithinPath">The in-path successor map being accumulated.</param>
    /// <param name="approvalPathEnds">The chain-end routing map being accumulated.</param>
    private static void ClassifyApprovalSteps(
        IReadOnlyList<ApprovalModel>? approvals,
        HashSet<string> offMainFlow,
        Dictionary<string, string> successorWithinPath,
        Dictionary<string, ApprovalPathEnd> approvalPathEnds)
    {
        if (approvals is null)
        {
            return;
        }

        foreach (var approval in approvals)
        {
            ClassifyApprovalPath(
                approval.RejectionSteps,
                approval.IsRejectionTerminal,
                approval.PrecedingStepName,
                offMainFlow,
                successorWithinPath,
                approvalPathEnds);

            ClassifyApprovalPath(
                approval.EscalationSteps,
                approval.IsEscalationTerminal,
                approval.PrecedingStepName,
                offMainFlow,
                successorWithinPath,
                approvalPathEnds);

            ClassifyApprovalSteps(
                approval.NestedEscalationApprovals,
                offMainFlow,
                successorWithinPath,
                approvalPathEnds);
        }
    }

    /// <summary>
    /// Classifies one approval rejection or escalation chain and records where its last step goes.
    /// </summary>
    /// <param name="pathSteps">The ordered steps of the chain, or null when none are declared.</param>
    /// <param name="endsWorkflow">Whether the chain declared that it ends the workflow.</param>
    /// <param name="approvalPrecedingStepName">The step the approval checkpoint follows.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    /// <param name="successorWithinPath">The in-path successor map being accumulated.</param>
    /// <param name="approvalPathEnds">The chain-end routing map being accumulated.</param>
    private static void ClassifyApprovalPath(
        IReadOnlyList<StepModel>? pathSteps,
        bool endsWorkflow,
        string approvalPrecedingStepName,
        HashSet<string> offMainFlow,
        Dictionary<string, string> successorWithinPath,
        Dictionary<string, ApprovalPathEnd> approvalPathEnds)
    {
        if (pathSteps is null || pathSteps.Count == 0)
        {
            return;
        }

        var pathStepNames = new List<string>(pathSteps.Count);
        foreach (var step in pathSteps)
        {
            pathStepNames.Add(step.StepName);
        }

        ClassifyPath(pathStepNames, offMainFlow, successorWithinPath);

        approvalPathEnds[pathStepNames[pathStepNames.Count - 1]] =
            new ApprovalPathEnd(endsWorkflow, approvalPrecedingStepName);
    }

    /// <summary>
    /// Classifies every lowered low-confidence handler step.
    /// </summary>
    /// <remarks>
    /// These steps carry no in-path successor here: a handler chain resolves its own routing,
    /// including whether its last step rejoins the main flow or terminates the workflow.
    /// </remarks>
    /// <param name="model">The workflow model.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    private static void ClassifyConfidenceHandlerSteps(WorkflowModel model, HashSet<string> offMainFlow)
    {
        if (model.ConfidenceHandlerStepNames is null)
        {
            return;
        }

        foreach (var stepName in model.ConfidenceHandlerStepNames)
        {
            offMainFlow.Add(stepName);
        }
    }

    /// <summary>
    /// Marks every step of one path off-main-flow and records each non-last step's in-path
    /// successor.
    /// </summary>
    /// <param name="pathStepNames">The ordered step phase names of a single path.</param>
    /// <param name="offMainFlow">The set being accumulated.</param>
    /// <param name="successorWithinPath">The in-path successor map being accumulated.</param>
    private static void ClassifyPath(
        IReadOnlyList<string> pathStepNames,
        HashSet<string> offMainFlow,
        Dictionary<string, string> successorWithinPath)
    {
        for (var i = 0; i < pathStepNames.Count; i++)
        {
            var stepName = pathStepNames[i];
            offMainFlow.Add(stepName);

            if (i < pathStepNames.Count - 1)
            {
                successorWithinPath[stepName] = pathStepNames[i + 1];
            }
        }
    }

    /// <summary>
    /// Where an approval's rejection or escalation chain goes once its last step completes.
    /// </summary>
    /// <param name="EndsWorkflow">
    /// Whether the chain declared that it ends the workflow rather than resuming the main flow.
    /// </param>
    /// <param name="ApprovalPrecedingStepName">
    /// The step the approval checkpoint follows; a resuming chain rejoins the main flow at the
    /// next main-flow step after it, which is the same target an approved decision resumes onto.
    /// </param>
    private readonly record struct ApprovalPathEnd(bool EndsWorkflow, string ApprovalPrecedingStepName);
}
