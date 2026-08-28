// -----------------------------------------------------------------------
// <copyright file="HandlerContext.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;

using Strategos.Generators.Models;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Provides contextual information for emitting saga handlers for a specific step.
/// </summary>
/// <remarks>
/// <para>
/// This record encapsulates all the adjacency and contextual information needed
/// to emit handlers for a workflow step, including its position in the workflow,
/// associated loops, branch information, and fork constructs.
/// </para>
/// <para>
/// By passing this context to handler emitters, we avoid recomputing lookup
/// information in each emitter and ensure consistent behavior.
/// </para>
/// </remarks>
/// <param name="StepIndex">The zero-based index of the step in the workflow.</param>
/// <param name="IsLastStep">Whether this is the last step in the workflow.</param>
/// <param name="IsTerminalStep">Whether this step is a terminal step that should mark the saga as completed.</param>
/// <param name="NextStepName">The name of the next step, or null if this is the last step.</param>
/// <param name="StepModel">The step model with validation info, or null if not available.</param>
/// <param name="LoopsAtStep">Loops that end at this step (ordered innermost to outermost), or null if none.</param>
/// <param name="BranchAtStep">Branch that begins after this step, or null if none.</param>
/// <param name="ApprovalAtStep">Approval checkpoint that follows this step, or null if none.</param>
/// <param name="ForkAtStep">Fork that begins after this step, or null if none.</param>
/// <param name="ForkPathEnding">Fork and path info if this step ends a fork path, or null if not in a path.</param>
/// <param name="JoinForkAtStep">Fork if this is the join step, or null if not a join step.</param>
/// <param name="IsForkPathStep">Whether this step is part of a fork path (used for fork-specific handler logic, not for command/event naming).</param>
/// <param name="ForkPathKey">
/// Routing key for this fork-path instance, or null when the step is not on a fork path.
/// Looked up with <see cref="PathRoutingKey.ForFork"/> so saga Handles bind
/// the same stem as <c>ForkPathCompletedNaming.For(model)</c>.
/// </param>
internal sealed record HandlerContext(
    int StepIndex,
    bool IsLastStep,
    bool IsTerminalStep,
    string? NextStepName,
    StepModel? StepModel,
    IReadOnlyList<LoopModel>? LoopsAtStep,
    BranchModel? BranchAtStep,
    ApprovalModel? ApprovalAtStep,
    ForkModel? ForkAtStep,
    (ForkModel Fork, ForkPathModel Path)? ForkPathEnding,
    ForkModel? JoinForkAtStep,
    bool IsForkPathStep,
    PathRoutingKey? ForkPathKey = null);
