// -----------------------------------------------------------------------
// <copyright file="SagaStepHandlersEmitter.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

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

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="sb"/> or <paramref name="model"/> is null.
    /// </exception>
    public void Emit(StringBuilder sb, WorkflowModel model)
    {
        ThrowHelper.ThrowIfNull(sb, nameof(sb));
        ThrowHelper.ThrowIfNull(model, nameof(model));

        var context = SagaEmissionContext.Create(model);

        for (int i = 0; i < model.StepNames.Count; i++)
        {
            var stepName = model.StepNames[i];
            var handlerContext = BuildHandlerContext(context, stepName, i);

            // Emit StartStep handler
            sb.AppendLine();
            _startEmitter.EmitHandler(sb, model, stepName, handlerContext);

            // Emit appropriate Completed handler based on context
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

        // Emit handlers for branch case steps
        // These steps execute conditionally based on discriminator
        if (model.HasBranches)
        {
            // Track underlying step names (not phase names) from main step loop
            // Phase names may have loop prefixes (e.g., TargetLoop_VerifyVetoWithResearchStep)
            // but event handlers use underlying step names (e.g., VerifyVetoWithResearchStep)
            // This prevents duplicate Handle(VerifyVetoWithResearchStepCompleted) methods
            var processedStepNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var phaseName in model.StepNames)
            {
                if (context.StepsByName.TryGetValue(phaseName, out var stepModel))
                {
                    processedStepNames.Add(stepModel.StepName);
                }
                else
                {
                    // Fallback: extract base step name from phase name (handles loop prefixes)
                    // e.g., "TargetLoop_VerifyVetoStep" → "VerifyVetoStep"
                    var baseStepName = ExtractBaseStepName(phaseName);
                    processedStepNames.Add(baseStepName);
                }
            }

            foreach (var branch in model.Branches!)
            {
                foreach (var branchCase in branch.Cases)
                {
                    for (var i = 0; i < branchCase.StepNames.Count; i++)
                    {
                        var stepName = branchCase.StepNames[i];

                        // Skip if already handled in main step loop
                        if (processedStepNames.Contains(stepName))
                        {
                            continue;
                        }

                        var isLastStepInBranchCase = i == branchCase.StepNames.Count - 1;

                        // Emit StartStep handler
                        sb.AppendLine();
                        var branchHandlerContext = new HandlerContext(
                            StepIndex: i,
                            IsLastStep: false, // Not last in overall workflow
                            IsTerminalStep: branchCase.IsTerminal && isLastStepInBranchCase,
                            NextStepName: isLastStepInBranchCase ? branch.RejoinStepName : branchCase.StepNames[i + 1],
                            StepModel: null,
                            LoopsAtStep: null,
                            BranchAtStep: null,
                            ApprovalAtStep: null,
                            ForkAtStep: null,
                            ForkPathEnding: null,
                            JoinForkAtStep: null,
                            IsForkPathStep: false);

                        _startEmitter.EmitHandler(sb, model, stepName, branchHandlerContext);

                        // Emit Completed handler
                        sb.AppendLine();
                        if (isLastStepInBranchCase)
                        {
                            // Last step in branch path - emit path end handler
                            _branchEmitter.EmitPathEndHandler(sb, model, stepName, branch, branchCase);
                        }
                        else
                        {
                            // Intermediate step - emit standard handler chaining to next step
                            _completedEmitter.EmitHandler(sb, model, stepName, branchHandlerContext);
                        }
                    }
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
        // Confidence handler steps (DR-5) are appended to StepNames for full lowering but are NOT
        // part of the main linear flow. They must not be a "next step" for the main flow. Compute
        // main-flow adjacency by skipping over them so the preceding main-flow step (e.g. a Finally)
        // keeps its terminal status instead of wrongly chaining into a handler step.
        var model = ctx.Model;
        var isConfidenceHandlerStep = model.IsConfidenceHandlerStep(stepName);

        string? nextStepName = null;
        if (!isConfidenceHandlerStep)
        {
            for (var j = index + 1; j < model.StepNames.Count; j++)
            {
                if (!model.IsConfidenceHandlerStep(model.StepNames[j]))
                {
                    nextStepName = model.StepNames[j];
                    break;
                }
            }
        }

        // The step is "last in the main flow" when no later main-flow step exists.
        var isLastStep = isConfidenceHandlerStep || nextStepName is null;

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

        // Check if this step ends a fork path
        (ForkModel Fork, ForkPathModel Path)? forkPathEnding = null;
        if (ctx.ForkPathInfo.TryGetValue(stepName, out var pathInfo))
        {
            forkPathEnding = pathInfo;
        }

        // Check if this step is part of a fork path (needs full step name for worker command)
        var isForkPathStep = ctx.ForkPathSteps.Contains(stepName);

        // Determine if this is a terminal step that should mark the saga as completed.
        // Terminal steps include: CompleteStep, FailedStep, TerminateStep, AutoFailStep.
        // Also check if this step is the last step in a branch path that ends with .Complete().
        var isTerminalStep = stepModel?.IsTerminal ?? IsTerminalStepName(stepName);

        // Check if this step is the last step in a branch path that ends with Complete()
        if (!isTerminalStep && ctx.BranchPathInfo.TryGetValue(stepName, out var branchPathInfo))
        {
            isTerminalStep = branchPathInfo.Case.IsTerminal;
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
        // 2. Branch point - step precedes a branch
        // 3. Branch path end - step is the last step in a branch path
        // 4. Fork point - step precedes a fork
        // 5. Fork path end - step is the last step in a fork path
        // 6. Standard - normal step completion

        if (handlerContext.LoopsAtStep is { Count: > 0 })
        {
            _loopCompletedEmitter.EmitHandler(sb, model, stepName, handlerContext);
        }
        else if (handlerContext.BranchAtStep is not null)
        {
            _branchEmitter.EmitRoutingHandler(sb, model, stepName, handlerContext.BranchAtStep);
        }
        else if (context.BranchPathInfo.TryGetValue(stepName, out var pathInfo))
        {
            _branchEmitter.EmitPathEndHandler(sb, model, stepName, pathInfo.Branch, pathInfo.Case);
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
}
