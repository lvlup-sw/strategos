// =============================================================================
// <copyright file="ApprovalDecision.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Models;

/// <summary>
/// The decision a human approver returns for an approval checkpoint. It is the discriminant carried by
/// the generated <c>Resume{ApprovalPoint}ApprovalCommand</c> and switched on by the saga's approval
/// resume handler to either proceed, fail, or continue awaiting.
/// </summary>
public enum ApprovalDecision
{
    /// <summary>
    /// The request was approved; the workflow proceeds to the next step (or completes if the approval
    /// is the final step).
    /// </summary>
    Approved,

    /// <summary>
    /// The request was rejected; the workflow transitions to its rejection path (or fails, if no
    /// rejection steps are configured).
    /// </summary>
    Rejected,

    /// <summary>
    /// No decision was reached; the workflow stays in the approval phase and awaits another response.
    /// </summary>
    Deferred
}
