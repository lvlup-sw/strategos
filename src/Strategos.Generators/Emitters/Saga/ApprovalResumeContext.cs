// -----------------------------------------------------------------------
// <copyright file="ApprovalResumeContext.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using Strategos.Generators.Models;

namespace Strategos.Generators.Emitters.Saga;

/// <summary>
/// Provides contextual information for emitting approval resume handlers.
/// </summary>
/// <remarks>
/// <para>
/// This record encapsulates the information needed to emit approval resume handlers,
/// specifically whether the approval is for the final step in the workflow and
/// what the next step name is (if any). When a fork or branch follows the checkpoint
/// immediately, those constructs are carried here so resume dispatches them instead
/// of the next main-flow step (#182).
/// </para>
/// </remarks>
/// <param name="IsLastStep">Whether the approval follows the last step in the workflow.</param>
/// <param name="NextStepName">The name of the next step, or null if this is the last step.</param>
/// <param name="ForkAtCheckpoint">The fork that follows this checkpoint, if any.</param>
/// <param name="BranchAtCheckpoint">The branch that follows this checkpoint, if any.</param>
internal sealed record ApprovalResumeContext(
    bool IsLastStep,
    string? NextStepName,
    ForkModel? ForkAtCheckpoint = null,
    BranchModel? BranchAtCheckpoint = null);
