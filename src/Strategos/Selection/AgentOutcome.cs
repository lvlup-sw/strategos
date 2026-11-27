// =============================================================================
// <copyright file="AgentOutcome.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Selection;

/// <summary>
/// Outcome of an agent execution for belief updates.
/// </summary>
/// <remarks>
/// <para>
/// Used to record the result of an agent handling a task, enabling the Thompson
/// Sampling selector to update its beliefs about agent performance.
/// </para>
/// <para>
/// The <see cref="Success"/> property is the primary signal used for belief updates.
/// Additional metadata like confidence, duration, and tokens are captured for
/// future enhancements (e.g., partial credit updates, cost-aware selection).
/// </para>
/// </remarks>
public sealed record AgentOutcome
{
    /// <summary>
    /// Gets a value indicating whether the agent succeeded on the task.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the primary signal for Bayesian belief updates:
    /// <list type="bullet">
    ///   <item><description>Success: Alpha is incremented by 1</description></item>
    ///   <item><description>Failure: Beta is incremented by 1</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets an optional confidence score from the agent (0-1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used as partial credit by <see cref="AgentBelief.WithOutcome"/>, which rejects
    /// a value that is NaN or outside [0, 1]. Callers forwarding an external score
    /// (for example an evaluator's output) must normalize it into that range first.
    /// </para>
    /// </remarks>
    public double? Confidence { get; init; }

    /// <summary>
    /// Gets an optional execution duration.
    /// </summary>
    /// <remarks>
    /// Reserved for future cost-aware selection that considers latency.
    /// </remarks>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Gets an optional count of tokens consumed.
    /// </summary>
    /// <remarks>
    /// Reserved for future cost-aware selection that considers token costs.
    /// </remarks>
    public int? TokensConsumed { get; init; }

    /// <summary>
    /// Creates a successful outcome.
    /// </summary>
    /// <param name="confidence">Optional confidence score (0-1).</param>
    /// <param name="duration">Optional execution duration.</param>
    /// <param name="tokensConsumed">Optional tokens consumed.</param>
    /// <returns>A successful agent outcome.</returns>
    public static AgentOutcome Succeeded(
        double? confidence = null,
        TimeSpan? duration = null,
        int? tokensConsumed = null)
    {
        return new AgentOutcome
        {
            Success = true,
            Confidence = confidence,
            Duration = duration,
            TokensConsumed = tokensConsumed,
        };
    }

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="confidence">Optional confidence score (0-1).</param>
    /// <param name="duration">Optional execution duration.</param>
    /// <param name="tokensConsumed">Optional tokens consumed.</param>
    /// <returns>A failed agent outcome.</returns>
    public static AgentOutcome Failed(
        double? confidence = null,
        TimeSpan? duration = null,
        int? tokensConsumed = null)
    {
        return new AgentOutcome
        {
            Success = false,
            Confidence = confidence,
            Duration = duration,
            TokensConsumed = tokensConsumed,
        };
    }
}
