// -----------------------------------------------------------------------
// <copyright file="SagaStepHandlersEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

using Strategos.Generators.Helpers;
using Strategos.Generators.Models;
using Strategos.Generators.Polyfills;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Emits step handler methods for all steps in a workflow saga.
/// </summary>
/// <remarks>
/// <para>
/// This emitter implements <see cref="ISagaComponentEmitter"/> to provide uniform
/// composition with other saga components. It orchestrates the emission of:
/// <list type="bullet">
///   <item><description>Start handlers for each step</description></item>
///   <item><description>Completed handlers (standard, loop, or branch) for each step</description></item>
/// </list>
/// </para>
/// <para>
/// The emitter uses <see cref="SagaEmissionContext"/> to build lookup dictionaries
/// and determine the appropriate handler type for each step based on its context
/// (loop body end, branch point, branch path end, or standard step).
/// </para>
/// </remarks>
internal sealed class SagaStepHandlersEmitter : ISagaComponentEmitter
{
    private readonly StepStartHandlerEmitter _startEmitter = new();
    private readonly StepCompletedHandlerEmitter _completedEmitter = new();
    private readonly LoopCompletedHandlerEmitter _loopCompletedEmitter = new();
    private readonly BranchHandlerEmitter _branchEmitter = new();
    private readonly ForkDispatchHandlerEmitter _forkDispatchEmitter = new();
    private readonly ForkJoinHandlerEmitter _forkJoinEmitter = new();
    private readonly DiagnosticForkHandlerEmitter _diagnosticForkEmitter = new();

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var context = SagaEmissionContext.Create(model);
        var emittedSharedBranchTypes = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < model.StepNames.Count; i++)
        {
            var stepName = model.StepNames[i];
            var handlerContext = BuildHandlerContext(context, stepName, i);

            sb.AppendLine();
            _startEmitter.EmitHandler(sb, model, stepName, handlerContext);

            var stepTypeName = handlerContext.StepModel?.StepName ?? ExtractBaseStepName(stepName);
            var occurrences = CollectBranchOccurrences(context, stepTypeName);
            if (occurrences.Count > 1)
            {
                if (emittedSharedBranchTypes.Add(stepTypeName))
                {
                    sb.AppendLine();
                    _branchEmitter.EmitLiveCaseCompletedHandler(
                        sb, model, $"{stepTypeName}Completed", occurrences);
                }

                continue;
            }

            sb.AppendLine();
            EmitCompletedHandler(sb, model, stepName, handlerContext, context);
        }

        // Emit fork-related handlers
        if (model.HasForks)
        {
            foreach (var fork in model.Forks!)
            {
                // Emit join readiness method
                _forkJoinEmitter.EmitJoinReadinessMethod(sb, fork);

                // Emit join handler
                _forkJoinEmitter.EmitJoinHandler(sb, model, fork);
            }
        }

        // Emit the diagnostic-fork decision-site handler (DR-9). A single
        // Handle(Fork{Pascal}Command) is the occurrence chokepoint that enforces the
        // anchor, permitted-trigger + evidence, and maxForks guards and seeds
        // compensation into the merged trigger site. No-op when the workflow declares
        // no AllowDiagnosticFork edge (byte-unchanged for non-fork workflows).
        _diagnosticForkEmitter.EmitDecisionSiteHandler(sb, model);

        // Emit handlers for branch case steps
        // These steps execute conditionally based on discriminator
        EmitDedicatedBranchCaseHandlers(sb, model, context, emittedSharedBranchTypes);
    }

    /// <summary>
    /// Emits start and completed handlers for branch-case steps that the main step loop did not
    /// already emit, including the cases of a branch a loop evaluates on exit.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="context">The saga emission context.</param>
    /// <param name="emittedSharedBranchTypes">Step types that already have a live-case completed handler.</param>
    /// <remarks>
    /// Loop-exit branches live on <see cref="LoopModel.BranchOnExit"/> and are deliberately
    /// absent from <see cref="WorkflowModel.Branches"/>. Walking only the workflow collection
    /// leaves a rejoining loop-exit case without a path-end handler, so the declared terminal
    /// never starts (#184).
    /// </remarks>
    private void EmitDedicatedBranchCaseHandlers(
        StringBuilder sb,
        WorkflowModel model,
        SagaEmissionContext context,
        HashSet<string> emittedSharedBranchTypes)
    {
        if (!model.HasBranches && !HasLoopExitBranch(model))
        {
            return;
        }

        // Track phase names already handled in the main step loop so instance-named
        // cases still get their own Start{PhaseName}Command handler, while a shared
        // completed Handle({StepType}Completed) is not emitted twice.
        var processedPhaseNames = new HashSet<string>(model.StepNames, StringComparer.Ordinal);

        if (model.HasBranches)
        {
            foreach (var branch in model.Branches!)
            {
                EmitHandlersForBranchCases(sb, model, context, branch, processedPhaseNames, emittedSharedBranchTypes);
            }
        }

        if (model.HasLoops)
        {
            foreach (var loop in model.Loops!)
            {
                if (loop.BranchOnExit is not null)
                {
                    EmitHandlersForBranchCases(sb, model, context, loop.BranchOnExit, processedPhaseNames, emittedSharedBranchTypes);
                }
            }
        }
    }

    /// <summary>
    /// Emits start and completed handlers for each unprocessed step of the branch's cases.
    /// </summary>
    /// <param name="sb">The <see cref="StringBuilder"/> to append generated code to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="context">The saga emission context.</param>
    /// <param name="branch">The branch whose cases should be emitted.</param>
    /// <param name="processedPhaseNames">Phase names already handled in the main step loop.</param>
    /// <param name="emittedSharedBranchTypes">Step types that already have a live-case completed handler.</param>
    private void EmitHandlersForBranchCases(
        StringBuilder sb,
        WorkflowModel model,
        SagaEmissionContext context,
        BranchModel branch,
        HashSet<string> processedPhaseNames,
        HashSet<string> emittedSharedBranchTypes)
    {
        foreach (var branchCase in branch.Cases)
        {
            for (var i = 0; i < branchCase.StepNames.Count; i++)
            {
                var stepName = branchCase.StepNames[i];
                var phaseName = ToPhaseName(branch.LoopPrefix, stepName);

                if (!processedPhaseNames.Add(phaseName))
                {
                    continue;
                }

                var isLastStepInBranchCase = i == branchCase.StepNames.Count - 1;
                var endsWorkflowHere = branchCase.IsTerminal && isLastStepInBranchCase;

                var nextStepName = isLastStepInBranchCase
                    ? (endsWorkflowHere ? null : branch.RejoinStepName)
                    : ToPhaseName(branch.LoopPrefix, branchCase.StepNames[i + 1]);

                context.StepsByName.TryGetValue(phaseName, out var stepModel);
                var stepTypeName = stepModel?.StepName ?? ExtractBaseStepName(stepName);

                sb.AppendLine();
                var branchHandlerContext = new HandlerContext(
                    StepIndex: i,
                    IsLastStep: false,
                    IsTerminalStep: endsWorkflowHere,
                    NextStepName: nextStepName,
                    StepModel: stepModel,
                    LoopsAtStep: null,
                    BranchAtStep: null,
                    ApprovalAtStep: null,
                    ForkAtStep: null,
                    ForkPathEnding: null,
                    JoinForkAtStep: null,
                    IsForkPathStep: false);

                _startEmitter.EmitHandler(sb, model, phaseName, branchHandlerContext);

                var occurrences = CollectBranchOccurrences(context, stepTypeName);
                if (occurrences.Count > 1)
                {
                    if (emittedSharedBranchTypes.Add(stepTypeName))
                    {
                        sb.AppendLine();
                        _branchEmitter.EmitLiveCaseCompletedHandler(
                            sb, model, $"{stepTypeName}Completed", occurrences);
                    }

                    continue;
                }

                sb.AppendLine();
                if (isLastStepInBranchCase)
                {
                    _branchEmitter.EmitPathEndHandler(
                        sb,
                        model,
                        phaseName,
                        branch,
                        branchCase,
                        stepModel?.Confidence,
                        stepTypeName);
                }
                else
                {
                    _completedEmitter.EmitHandler(sb, model, phaseName, branchHandlerContext);
                }
            }
        }
    }

    /// <summary>
    /// Builds the handler context for a specific step.
    /// </summary>
    /// <param name="ctx">The saga emission context.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="index">The zero-based index of the step.</param>
    /// <returns>A handler context containing adjacency and contextual information.</returns>
    private static HandlerContext BuildHandlerContext(
        SagaEmissionContext ctx,
        string stepName,
        int index)
    {
        // Several lowering blocks append names to StepNames for full lowering (phase, worker
        // handler, commands, events) even though the steps are reached through their own
        // construct and never by main-flow chaining. Main-flow adjacency therefore comes from
        // the shared classification, not from list position, so the preceding main-flow step
        // (e.g. a Finally) keeps its terminal status instead of chaining into an appended step.
        var model = ctx.Model;
        var mainFlow = ctx.MainFlow;
        var isConfidenceHandlerStep = model.IsConfidenceHandlerStep(stepName);

        string? nextStepName;
        bool isLastStep;

        if (!mainFlow.IsOffMainFlow(stepName))
        {
            // On the main flow: chain to the next entry that is also on the main flow.
            nextStepName = mainFlow.NextMainFlowStepNameAfterIndex(index);
            isLastStep = nextStepName is null;
        }
        else if (TryGetKeyedSuccessor(ctx, stepName, out var successorWithinPath))
        {
            // Inside a fork path or a branch case, and not its last step. Look up by
            // PathRoutingKey so two paths that share a phase name keep their own successor.
            nextStepName = successorWithinPath;
            isLastStep = false;
        }
        else if (mainFlow.TryGetApprovalPathEndSuccessor(stepName, out var approvalResumeStepName))
        {
            // The last step of an approval's rejection or escalation chain. Unlike a fork path or
            // a branch case, no dedicated path-end handler intercepts it — the approval component
            // dispatches the chain's first step through the GENERIC start command, so the generic
            // completed handler is the chain's only routing site. A chain that declared its own
            // completion ends the workflow here; one that did not resumes the main flow where an
            // approved decision would have.
            nextStepName = approvalResumeStepName;
            isLastStep = approvalResumeStepName is null;
        }
        else
        {
            // A path's last step or a failure-handler step. The construct that owns the step emits
            // its completed handler, so there is no main-flow successor to resolve.
            nextStepName = null;
            isLastStep = true;
        }

        // Confidence handler CHAIN routing (G-4 / #139). A handler step is NOT unconditionally
        // terminal: it chains to the next step in its OnLowConfidence chain when one exists, and the
        // chain's LAST step either rejoins the main flow (.RejoinMainFlow()) at the step after the
        // gated step, or terminates the workflow (back-compat default). Only the terminating last
        // step is treated as "last" so the completed-handler emitter marks the saga completed; a
        // chaining or rejoining step gets a concrete next-step command.
        if (isConfidenceHandlerStep)
        {
            var (nextHandlerStepName, isLastInChain, rejoinStepName) =
                model.GetConfidenceHandlerChainRouting(stepName);

            if (!isLastInChain)
            {
                // Mid-chain: chain to the next handler step.
                nextStepName = nextHandlerStepName;
                isLastStep = false;
            }
            else if (rejoinStepName is not null)
            {
                // Terminal step of a REJOINING chain: resume the main flow.
                nextStepName = rejoinStepName;
                isLastStep = false;
            }
            else
            {
                // Terminal step of a TERMINATING chain (default): end the workflow.
                nextStepName = null;
                isLastStep = true;
            }
        }

        ctx.LoopsByLastStep.TryGetValue(stepName, out var loopsAtStep);
        ctx.BranchesByPreviousStep.TryGetValue(stepName, out var branchAtStep);
        ctx.StepsByName.TryGetValue(stepName, out var stepModel);
        ctx.ApprovalsByPrecedingStep.TryGetValue(stepName, out var approvalAtStep);
        ctx.ForksByPreviousStep.TryGetValue(stepName, out var forkAtStep);
        ctx.ForksByJoinStep.TryGetValue(stepName, out var joinForkAtStep);

        // Check if this step ends a fork path. Key by routing key, not the
        // last-write-wins string ForkPathInfo map.
        (ForkModel Fork, ForkPathModel Path)? forkPathEnding = null;
        foreach (var entry in ctx.ForkPathsByRoutingKey)
        {
            if (string.Equals(entry.Key.PhaseName, stepName, StringComparison.Ordinal))
            {
                forkPathEnding = entry.Value;
                break;
            }
        }

        // Check if this step is part of a fork path (needs full step name for worker command)
        var isForkPathStep = ctx.ForkPathSteps.Contains(stepName);

        // Determine if this is a terminal step that should mark the saga as completed.
        // Terminal steps include: CompleteStep, FailedStep, TerminateStep, AutoFailStep.
        // Also check if this step is the last step in a branch path that ends with .Complete().
        var isTerminalStep = stepModel?.IsTerminal ?? IsTerminalStepName(stepName);

        if (!isTerminalStep)
        {
            foreach (var entry in ctx.BranchPathsByRoutingKey)
            {
                if (string.Equals(entry.Key.PhaseName, stepName, StringComparison.Ordinal))
                {
                    isTerminalStep = entry.Value.Case.IsTerminal;
                    break;
                }
            }
        }

        return new HandlerContext(
            StepIndex: index,
            IsLastStep: isLastStep,
            IsTerminalStep: isTerminalStep,
            NextStepName: nextStepName,
            StepModel: stepModel,
            LoopsAtStep: loopsAtStep,
            BranchAtStep: branchAtStep,
            ApprovalAtStep: approvalAtStep,
            ForkAtStep: forkAtStep,
            ForkPathEnding: forkPathEnding,
            JoinForkAtStep: joinForkAtStep,
            IsForkPathStep: isForkPathStep);
    }

    /// <summary>
    /// Checks if a step name indicates a terminal step by convention.
    /// </summary>
    /// <param name="stepName">The name of the step.</param>
    /// <returns>True if the step name indicates a terminal step; otherwise, false.</returns>
    private static bool IsTerminalStepName(string stepName)
    {
        return stepName is "CompleteStep" or "FailedStep" or "TerminateStep" or "AutoFailStep";
    }

    /// <summary>
    /// Emits the appropriate completed handler based on the step's context.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="model">The workflow model.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="handlerContext">The handler context for this step.</param>
    /// <param name="context">The saga emission context.</param>
    private void EmitCompletedHandler(
        StringBuilder sb,
        WorkflowModel model,
        string stepName,
        HandlerContext handlerContext,
        SagaEmissionContext context)
    {
        // Priority order:
        // 1. Loop end - step is the last step in a loop body
        // 2. Approval checkpoint - park until a decision (#182). ForkExtractor and
        //    BranchExtractor walk through AwaitApproval, so ForkAtStep / BranchAtStep
        //    share the gated step's name. Dispatch after resume, not here.
        // 3. Branch point - step precedes a branch
        // 4. Branch path end - step is the last step in a branch path
        // 5. Fork point - step precedes a fork
        // 6. Fork path end - step is the last step in a fork path
        // 7. Standard - normal step completion

        if (handlerContext.LoopsAtStep is { Count: > 0 })
        {
            _loopCompletedEmitter.EmitHandler(sb, model, stepName, handlerContext);
        }
        else if (handlerContext.ApprovalAtStep is not null)
        {
            _completedEmitter.EmitHandler(sb, model, stepName, handlerContext);
        }
        else if (handlerContext.BranchAtStep is not null)
        {
            _branchEmitter.EmitRoutingHandler(sb, model, stepName, handlerContext.BranchAtStep);
        }
        else if (TryGetBranchPathEnd(context, stepName, out var pathInfo))
        {
            _branchEmitter.EmitPathEndHandler(
                sb,
                model,
                stepName,
                pathInfo.Branch,
                pathInfo.Case,
                handlerContext.StepModel?.Confidence,
                handlerContext.StepModel?.StepName);
        }
        else if (handlerContext.ForkAtStep is not null)
        {
            _forkDispatchEmitter.EmitDispatchHandler(sb, model, stepName, handlerContext.ForkAtStep);
        }
        else if (handlerContext.ForkPathEnding is not null)
        {
            var (fork, path) = handlerContext.ForkPathEnding.Value;
            _forkJoinEmitter.EmitPathCompletedHandler(sb, model, stepName, fork, path);
        }
        else
        {
            _completedEmitter.EmitHandler(sb, model, stepName, handlerContext);
        }
    }

    /// <summary>
    /// Collects every branch-case occupancy of a step type so a shared completed
    /// handler can route by the live case.
    /// </summary>
    private static List<BranchCaseStepOccurrence> CollectBranchOccurrences(
        SagaEmissionContext context,
        string stepTypeName)
    {
        var occurrences = new List<BranchCaseStepOccurrence>();
        foreach (var branch in EnumerateBranches(context.Model))
        {
            foreach (var branchCase in branch.Cases)
            {
                for (var i = 0; i < branchCase.StepNames.Count; i++)
                {
                    var phaseName = ToPhaseName(branch.LoopPrefix, branchCase.StepNames[i]);
                    context.StepsByName.TryGetValue(phaseName, out var stepModel);
                    var typeName = stepModel?.StepName ?? ExtractBaseStepName(branchCase.StepNames[i]);
                    if (!string.Equals(typeName, stepTypeName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var isLast = i == branchCase.StepNames.Count - 1;
                    string? successor = null;
                    if (!isLast)
                    {
                        successor = ToPhaseName(branch.LoopPrefix, branchCase.StepNames[i + 1]);
                    }
                    else if (!branchCase.IsTerminal && branch.HasRejoinPoint)
                    {
                        successor = branch.RejoinStepName;
                    }

                    occurrences.Add(new BranchCaseStepOccurrence(
                        branch, branchCase, phaseName, successor));
                }
            }
        }

        return occurrences;
    }

    private static IEnumerable<BranchModel> EnumerateBranches(WorkflowModel model)
    {
        if (model.HasBranches)
        {
            foreach (var branch in model.Branches!)
            {
                yield return branch;
            }
        }

        if (!model.HasLoops)
        {
            yield break;
        }

        foreach (var loop in model.Loops!)
        {
            if (loop.BranchOnExit is not null)
            {
                yield return loop.BranchOnExit;
            }
        }
    }

    private static bool TryGetKeyedSuccessor(
        SagaEmissionContext ctx,
        string stepName,
        out string successorStepName)
    {
        foreach (var entry in ctx.ForkPathsByRoutingKey)
        {
            var key = PathRoutingKey.ForFork(entry.Value.Fork.ForkId, entry.Value.Path.PathIndex, stepName);
            if (ctx.MainFlow.TryGetSuccessorWithinPath(key, out successorStepName))
            {
                return true;
            }
        }

        foreach (var entry in ctx.BranchPathsByRoutingKey)
        {
            var key = PathRoutingKey.ForBranch(
                entry.Value.Branch.BranchId,
                entry.Value.Case.BranchPathPrefix,
                stepName);
            if (ctx.MainFlow.TryGetSuccessorWithinPath(key, out successorStepName))
            {
                return true;
            }
        }

        return ctx.MainFlow.TryGetSuccessorWithinPath(stepName, out successorStepName);
    }

    private static bool TryGetBranchPathEnd(
        SagaEmissionContext context,
        string stepName,
        out (BranchModel Branch, BranchCaseModel Case) pathInfo)
    {
        foreach (var entry in context.BranchPathsByRoutingKey)
        {
            if (string.Equals(entry.Key.PhaseName, stepName, StringComparison.Ordinal))
            {
                pathInfo = entry.Value;
                return true;
            }
        }

        pathInfo = default;
        return false;
    }

    private static string ToPhaseName(string? loopPrefix, string effectiveName) =>
        string.IsNullOrEmpty(loopPrefix) ? effectiveName : $"{loopPrefix}_{effectiveName}";

    /// <summary>
    /// Extracts the base step name from a phase name that may include loop prefixes.
    /// </summary>
    /// <param name="phaseName">The phase name (e.g., "TargetLoop_VerifyVetoStep").</param>
    /// <returns>The base step name (e.g., "VerifyVetoStep").</returns>
    /// <remarks>
    /// Phase names for loop steps follow the pattern "{LoopName}_{StepName}".
    /// For nested loops, the pattern is "{OuterLoop}_{InnerLoop}_{StepName}".
    /// This method extracts the step name by taking the part after the last underscore.
    /// </remarks>
    private static string ExtractBaseStepName(string phaseName)
    {
        var lastUnderscoreIndex = phaseName.LastIndexOf('_');
        return lastUnderscoreIndex >= 0
            ? phaseName.Substring(lastUnderscoreIndex + 1)
            : phaseName;
    }

    /// <summary>
    /// Returns whether any loop on the model evaluates a branch on exit.
    /// </summary>
    /// <param name="model">The workflow model.</param>
    /// <returns>
    /// <see langword="true"/> when at least one loop carries a <see cref="LoopModel.BranchOnExit"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasLoopExitBranch(WorkflowModel model)
    {
        if (!model.HasLoops)
        {
            return false;
        }

        foreach (var loop in model.Loops!)
        {
            if (loop.BranchOnExit is not null)
            {
                return true;
            }
        }

        return false;
    }
}
