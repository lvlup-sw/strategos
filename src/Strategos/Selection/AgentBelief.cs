// =============================================================================
// <copyright file="AgentBelief.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Selection;

/// <summary>
/// Represents the Beta distribution belief state for an (agent, task category) pair
/// in Thompson Sampling agent selection.
/// </summary>
/// <remarks>
/// <para>
/// The belief models the probability of success when an agent handles a task category:
/// <c>θ ~ Beta(α, β)</c>.
/// </para>
/// <para>
/// Where:
/// <list type="bullet">
///   <item><description><c>α</c> (Alpha) - Pseudo-count of successes plus prior</description></item>
///   <item><description><c>β</c> (Beta) - Pseudo-count of failures plus prior</description></item>
/// </list>
/// </para>
/// <para>
/// The default prior of Beta(2, 2) is weakly informative, centered at 0.5 with
/// moderate uncertainty. This allows quick adaptation to observed outcomes while
/// avoiding extreme initial estimates.
/// </para>
/// </remarks>
public sealed record AgentBelief
{
    /// <summary>
    /// The default prior value for Alpha (success pseudo-count).
    /// </summary>
    public const double DefaultPriorAlpha = 2.0;

    /// <summary>
    /// The default prior value for Beta (failure pseudo-count).
    /// </summary>
    public const double DefaultPriorBeta = 2.0;

    /// <summary>
    /// Gets the document ID for Marten persistence.
    /// </summary>
    /// <remarks>
    /// Composite key of "{AgentId}_{TaskCategory}" for unique identification.
    /// </remarks>
    public string Id => $"{AgentId}_{TaskCategory}";

    /// <summary>
    /// Gets the agent identifier this belief applies to.
    /// </summary>
    public required string AgentId { get; init; }

    /// <summary>
    /// Gets the task category this belief applies to.
    /// </summary>
    public required string TaskCategory { get; init; }

    /// <summary>
    /// Gets the Alpha parameter (pseudo-count of successes plus prior).
    /// </summary>
    /// <remarks>
    /// Incremented by 1 on each observed success. Higher values indicate
    /// higher expected success rate.
    /// </remarks>
    public double Alpha { get; init; } = DefaultPriorAlpha;

    /// <summary>
    /// Gets the Beta parameter (pseudo-count of failures plus prior).
    /// </summary>
    /// <remarks>
    /// Incremented by 1 on each observed failure. Higher values indicate
    /// lower expected success rate.
    /// </remarks>
    public double Beta { get; init; } = DefaultPriorBeta;

    /// <summary>
    /// Gets the number of actual observations (not including prior).
    /// </summary>
    public int ObservationCount { get; init; }

    /// <summary>
    /// Gets the timestamp of the last belief update.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the mean of the Beta distribution: α / (α + β).
    /// </summary>
    /// <remarks>
    /// Represents the expected probability of success for this agent
    /// on this task category, given observed outcomes.
    /// </remarks>
    public double Mean => Alpha / (Alpha + Beta);

    /// <summary>
    /// Gets the variance of the Beta distribution: αβ / ((α+β)²(α+β+1)).
    /// </summary>
    /// <remarks>
    /// Lower variance indicates higher confidence in the mean estimate.
    /// Variance decreases as more observations are collected.
    /// </remarks>
    public double Variance
    {
        get
        {
            var sum = Alpha + Beta;
            return (Alpha * Beta) / (sum * sum * (sum + 1));
        }
    }

    /// <summary>
    /// Creates a new belief with uninformative prior for the given agent and task category.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="taskCategory">The task category.</param>
    /// <returns>A new belief with default prior parameters.</returns>
    public static AgentBelief CreatePrior(string agentId, string taskCategory)
    {
        return new AgentBelief
        {
            AgentId = agentId,
            TaskCategory = taskCategory,
            Alpha = DefaultPriorAlpha,
            Beta = DefaultPriorBeta,
            ObservationCount = 0,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Returns a new belief with Alpha incremented by 1 (success observed).
    /// </summary>
    /// <returns>A new belief with updated Alpha and ObservationCount.</returns>
    public AgentBelief WithSuccess()
    {
        return this with
        {
            Alpha = Alpha + 1.0,
            ObservationCount = ObservationCount + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Returns a new belief with Beta incremented by 1 (failure observed).
    /// </summary>
    /// <returns>A new belief with updated Beta and ObservationCount.</returns>
    public AgentBelief WithFailure()
    {
        return this with
        {
            Beta = Beta + 1.0,
            ObservationCount = ObservationCount + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Returns a new belief updated based on the agent outcome with optional partial credit.
    /// </summary>
    /// <param name="outcome">The outcome of the agent execution.</param>
    /// <returns>A new belief with updated Alpha, Beta, and ObservationCount.</returns>
    /// <remarks>
    /// <para>
    /// When <see cref="AgentOutcome.Confidence"/> is provided, it is used as a partial credit
    /// factor for the update:
    /// <list type="bullet">
    ///   <item><description>Alpha incremented by confidence value</description></item>
    ///   <item><description>Beta incremented by (1 - confidence)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// When confidence is not provided, the update uses binary credit based on
    /// <see cref="AgentOutcome.Success"/>:
    /// <list type="bullet">
    ///   <item><description>Success: Alpha + 1, Beta unchanged</description></item>
    ///   <item><description>Failure: Alpha unchanged, Beta + 1</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="outcome"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <see cref="AgentOutcome.Confidence"/> is NaN or outside [0, 1].
    /// </exception>
    public AgentBelief WithOutcome(AgentOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome, nameof(outcome));

        // Use confidence for partial credit if available, otherwise binary credit
        var credit = outcome.Confidence ?? (outcome.Success ? 1.0 : 0.0);

        // Written as a negated range test so NaN is rejected too. Credit outside [0, 1]
        // would drive Alpha or Beta non-positive, which no Beta distribution accepts.
        if (!(credit >= 0.0 && credit <= 1.0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                credit,
                "Outcome confidence must be between 0 and 1.");
        }

        return this with
        {
            Alpha = Alpha + credit,
            Beta = Beta + (1.0 - credit),
            ObservationCount = ObservationCount + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
