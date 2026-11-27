// =============================================================================
// <copyright file="DefaultBeliefPriorFactory.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Selection;

namespace Strategos.Infrastructure.Selection;

/// <summary>
/// Default implementation of <see cref="IBeliefPriorFactory"/> using configurable
/// Beta distribution parameters.
/// </summary>
/// <remarks>
/// <para>
/// This factory creates priors with fixed Alpha and Beta parameters regardless of
/// task features. For MVP, this provides a simple starting point while enabling
/// future extensions that adjust priors based on task complexity or other features.
/// </para>
/// <para>
/// Common prior configurations:
/// <list type="bullet">
///   <item><description>Beta(2, 2) - Weakly informative, centered at 0.5 (default)</description></item>
///   <item><description>Beta(1, 1) - Uniform prior, no initial bias</description></item>
///   <item><description>Beta(5, 2) - Optimistic prior, expects higher success rate</description></item>
///   <item><description>Beta(2, 5) - Pessimistic prior, expects lower success rate</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class DefaultBeliefPriorFactory : IBeliefPriorFactory
{
    private readonly double _alpha;
    private readonly double _beta;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBeliefPriorFactory"/> class
    /// with default Beta(2, 2) prior parameters.
    /// </summary>
    public DefaultBeliefPriorFactory()
        : this(AgentBelief.DefaultPriorAlpha, AgentBelief.DefaultPriorBeta)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultBeliefPriorFactory"/> class
    /// with custom prior parameters.
    /// </summary>
    /// <param name="alpha">The Alpha parameter for the Beta distribution (must be positive).</param>
    /// <param name="beta">The Beta parameter for the Beta distribution (must be positive).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="alpha"/> or <paramref name="beta"/> is not finite and positive.
    /// </exception>
    public DefaultBeliefPriorFactory(double alpha, double beta)
    {
        ThrowIfInvalidShape(alpha, nameof(alpha));
        ThrowIfInvalidShape(beta, nameof(beta));

        _alpha = alpha;
        _beta = beta;
    }

    /// <inheritdoc/>
    public AgentBelief CreatePrior(string agentId, TaskFeatures features)
    {
        ArgumentNullException.ThrowIfNull(agentId, nameof(agentId));
        ArgumentNullException.ThrowIfNull(features, nameof(features));

        return new AgentBelief
        {
            AgentId = agentId,
            TaskCategory = features.Category.ToString(),
            Alpha = _alpha,
            Beta = _beta,
            ObservationCount = 0,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static void ThrowIfInvalidShape(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Prior parameters must be finite and positive.");
        }
    }
}
