// =============================================================================
// <copyright file="ThompsonSamplingAgentSelector.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Primitives;
using Strategos.Selection;

namespace Strategos.Infrastructure.Selection;

/// <summary>
/// Thompson Sampling implementation of <see cref="IAgentSelector"/> for
/// contextual bandit-based agent selection.
/// </summary>
/// <remarks>
/// <para>
/// Uses Beta-Bernoulli Thompson Sampling to balance exploration and exploitation
/// when selecting agents for task execution. Each (agent, task category) pair
/// maintains a Beta distribution belief that is updated based on observed outcomes.
/// </para>
/// <para>
/// Selection process:
/// <list type="number">
///   <item><description>Classify task description into a <see cref="TaskCategory"/></description></item>
///   <item><description>For each candidate agent, sample θ from Beta(α, β)</description></item>
///   <item><description>Select the agent with the highest sampled θ</description></item>
/// </list>
/// </para>
/// <para>
/// The sampling naturally balances exploration (trying uncertain agents) with
/// exploitation (using agents with high observed success rates).
/// </para>
/// </remarks>
public sealed class ThompsonSamplingAgentSelector : IAgentSelector
{
    private readonly IBeliefStore _beliefStore;
    private readonly ITaskCategoryClassifier _classifier;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThompsonSamplingAgentSelector"/> class.
    /// </summary>
    /// <param name="beliefStore">The belief store for persisting agent beliefs.</param>
    /// <param name="classifier">The task category classifier.</param>
    /// <param name="randomSeed">Optional seed for reproducible sampling.</param>
    public ThompsonSamplingAgentSelector(
        IBeliefStore beliefStore,
        ITaskCategoryClassifier classifier,
        int? randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(beliefStore);
        ArgumentNullException.ThrowIfNull(classifier);
        _beliefStore = beliefStore;
        _classifier = classifier;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    /// <inheritdoc/>
    public async Task<Result<AgentSelection>> SelectAgentAsync(
        AgentSelectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Classify task
        var taskCategory = _classifier.Classify(context.TaskDescription);
        var categoryName = taskCategory.ToString();

        // 2. Get available agents (exclude any excluded)
        // Optimization: Skip .Except() allocation when no exclusions
        var candidates = context.ExcludedAgents is { Count: > 0 }
            ? context.AvailableAgents.Except(context.ExcludedAgents).ToList()
            : context.AvailableAgents.ToList();

        if (candidates.Count == 0)
        {
            return Result<AgentSelection>.Failure(Error.Create(
                ErrorType.Validation,
                "SELECTOR_NO_CANDIDATES",
                "No available agents after applying exclusions"));
        }

        // 3. Fetch beliefs in parallel for all candidates
        // Convert ValueTask to Task for Task.WhenAll compatibility
        var beliefTasks = candidates.Select(agentId =>
            _beliefStore.GetBeliefAsync(agentId, categoryName, cancellationToken).AsTask());
        var beliefResults = await Task.WhenAll(beliefTasks).ConfigureAwait(false);

        // 4. Sample from Beta posteriors for each candidate
        var bestAgentId = candidates[0];
        var bestTheta = double.MinValue;
        var bestBelief = AgentBelief.CreatePrior(bestAgentId, categoryName);

        for (int i = 0; i < candidates.Count; i++)
        {
            var beliefResult = beliefResults[i];
            var agentId = candidates[i];

            var belief = beliefResult.IsSuccess
                ? beliefResult.Value
                : AgentBelief.CreatePrior(agentId, categoryName);

            var theta = SampleBeta(belief.Alpha, belief.Beta);

            if (theta > bestTheta)
            {
                bestTheta = theta;
                bestAgentId = agentId;
                bestBelief = belief;
            }
        }

        // 5. Compute confidence based on observation count
        var confidence = Math.Min(1.0, bestBelief.ObservationCount / 20.0);

        return Result<AgentSelection>.Success(new AgentSelection
        {
            SelectedAgentId = bestAgentId,
            TaskCategory = taskCategory,
            SampledTheta = bestTheta,
            SelectionConfidence = confidence,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<Unit>> RecordOutcomeAsync(
        string agentId,
        string taskCategory,
        AgentOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(taskCategory);
        ArgumentNullException.ThrowIfNull(outcome);

        return await _beliefStore.UpdateBeliefAsync(
            agentId,
            taskCategory,
            outcome.Success,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Samples from a Beta(alpha, beta) distribution using the gamma distribution method.
    /// </summary>
    /// <param name="alpha">The alpha (success) parameter.</param>
    /// <param name="beta">The beta (failure) parameter.</param>
    /// <returns>A sample from Beta(alpha, beta) in the range [0, 1].</returns>
    /// <remarks>
    /// Uses the relationship: if X ~ Gamma(α) and Y ~ Gamma(β) are independent,
    /// then X / (X + Y) ~ Beta(α, β).
    /// </remarks>
    private double SampleBeta(double alpha, double beta)
    {
        var x = SampleGamma(alpha);
        var y = SampleGamma(beta);

        // Handle edge case where both are very small
        if (x + y < double.Epsilon)
        {
            return 0.5;
        }

        return x / (x + y);
    }

    /// <summary>
    /// Samples from a Gamma(shape) distribution with scale=1 using Marsaglia and Tsang's method.
    /// </summary>
    /// <param name="shape">The shape parameter (α).</param>
    /// <returns>A sample from Gamma(shape, 1).</returns>
    /// <remarks>
    /// Implements Marsaglia and Tsang's method for shape >= 1.
    /// For shape less than 1, uses the transformation: if X ~ Gamma(shape + 1, 1),
    /// then X * U^(1/shape) ~ Gamma(shape, 1) where U ~ Uniform(0, 1).
    /// </remarks>
    private double SampleGamma(double shape)
    {
        // For shape < 1, use the transformation
        if (shape < 1)
        {
            return SampleGamma(shape + 1) * Math.Pow(_random.NextDouble(), 1.0 / shape);
        }

        // Marsaglia and Tsang's method for shape >= 1
        var d = shape - (1.0 / 3.0);
        var c = 1.0 / Math.Sqrt(9.0 * d);

        while (true)
        {
            double x;
            double v;

            do
            {
                x = SampleStandardNormal();
                v = 1.0 + (c * x);
            }
            while (v <= 0);

            v = v * v * v;
            var u = _random.NextDouble();

            // Quick acceptance check
            if (u < 1.0 - (0.0331 * x * x * x * x))
            {
                return d * v;
            }

            // Full acceptance check
            if (Math.Log(u) < (0.5 * x * x) + (d * (1.0 - v + Math.Log(v))))
            {
                return d * v;
            }
        }
    }

    /// <summary>
    /// Samples from a standard normal distribution using Box-Muller transform.
    /// </summary>
    /// <returns>A sample from N(0, 1).</returns>
    private double SampleStandardNormal()
    {
        var u1 = 1.0 - _random.NextDouble(); // Avoid log(0)
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
