// =============================================================================
// <copyright file="ContextualAgentSelector.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using Strategos.Abstractions;
using Strategos.Primitives;
using Strategos.Selection;

namespace Strategos.Infrastructure.Selection;

/// <summary>
/// Contextual Thompson Sampling implementation of <see cref="IAgentSelector"/> with
/// feature extraction and configurable priors.
/// </summary>
/// <remarks>
/// <para>
/// Extends the basic Thompson Sampling approach with:
/// <list type="bullet">
///   <item><description>Feature extraction via <see cref="ITaskFeatureExtractor"/></description></item>
///   <item><description>Configurable priors via <see cref="IBeliefPriorFactory"/></description></item>
///   <item><description>Partial credit updates via <see cref="AgentBelief.WithOutcome"/></description></item>
///   <item><description>Rich selection diagnostics via <see cref="AgentSelection.Features"/></description></item>
/// </list>
/// </para>
/// <para>
/// Selection process:
/// <list type="number">
///   <item><description>Extract task features using the feature extractor</description></item>
///   <item><description>For each candidate, get belief or create prior using factory</description></item>
///   <item><description>Sample θ from Beta(α, β) for each candidate</description></item>
///   <item><description>Select the candidate with highest sampled θ</description></item>
///   <item><description>Return selection with extracted features for diagnostics</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class ContextualAgentSelector : IAgentSelector
{
    private readonly IBeliefStore _beliefStore;
    private readonly ITaskFeatureExtractor _featureExtractor;
    private readonly IBeliefPriorFactory _priorFactory;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextualAgentSelector"/> class.
    /// </summary>
    /// <param name="beliefStore">The belief store for persisting agent beliefs.</param>
    /// <param name="featureExtractor">The feature extractor for task classification.</param>
    /// <param name="priorFactory">The factory for creating prior beliefs.</param>
    /// <param name="randomSeed">Optional seed for reproducible sampling.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any required parameter is null.
    /// </exception>
    public ContextualAgentSelector(
        IBeliefStore beliefStore,
        ITaskFeatureExtractor featureExtractor,
        IBeliefPriorFactory priorFactory,
        int? randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(beliefStore, nameof(beliefStore));
        ArgumentNullException.ThrowIfNull(featureExtractor, nameof(featureExtractor));
        ArgumentNullException.ThrowIfNull(priorFactory, nameof(priorFactory));

        _beliefStore = beliefStore;
        _featureExtractor = featureExtractor;
        _priorFactory = priorFactory;
        _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
    }

    /// <inheritdoc/>
    public async Task<Result<AgentSelection>> SelectAgentAsync(
        AgentSelectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        // 1. Extract features using the feature extractor
        var features = _featureExtractor.ExtractFeatures(context);
        var categoryName = features.Category.ToString();

        // 2. Get available agents (exclude any excluded)
        var candidates = context.AvailableAgents
            .Except(context.ExcludedAgents ?? [])
            .ToList();

        if (candidates.Count == 0)
        {
            return Result<AgentSelection>.Failure(Error.Create(
                ErrorType.Validation,
                "SELECTOR_NO_CANDIDATES",
                "No available agents after applying exclusions"));
        }

        // 3. Sample from Beta posteriors for each candidate
        string? bestAgentId = null;
        var bestTheta = double.MinValue;
        AgentBelief? bestBelief = null;

        foreach (var agentId in candidates)
        {
            var beliefResult = await _beliefStore.GetBeliefAsync(agentId, categoryName, cancellationToken)
                .ConfigureAwait(false);

            // Use existing belief if found with observations, otherwise create prior using factory
            AgentBelief belief;
            if (beliefResult.IsSuccess && beliefResult.Value.ObservationCount > 0)
            {
                belief = beliefResult.Value;
            }
            else
            {
                belief = _priorFactory.CreatePrior(agentId, features);
            }

            var theta = SampleBeta(belief.Alpha, belief.Beta);

            if (theta > bestTheta)
            {
                bestTheta = theta;
                bestAgentId = agentId;
                bestBelief = belief;
            }
        }

        // Ensure we have a selection (candidates was verified non-empty above)
        bestAgentId ??= candidates[0];
        bestBelief ??= _priorFactory.CreatePrior(bestAgentId, features);

        // 4. Compute confidence based on observation count
        var confidence = Math.Min(1.0, bestBelief.ObservationCount / 20.0);

        return Result<AgentSelection>.Success(new AgentSelection
        {
            SelectedAgentId = bestAgentId,
            TaskCategory = features.Category,
            SampledTheta = bestTheta,
            SelectionConfidence = confidence,
            Features = features,
        });
    }

    /// <inheritdoc/>
    public async Task<Result<Unit>> RecordOutcomeAsync(
        string agentId,
        string taskCategory,
        AgentOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentId, nameof(agentId));
        ArgumentNullException.ThrowIfNull(taskCategory, nameof(taskCategory));
        ArgumentNullException.ThrowIfNull(outcome, nameof(outcome));

        // Get existing belief or create default prior
        var beliefResult = await _beliefStore.GetBeliefAsync(agentId, taskCategory, cancellationToken)
            .ConfigureAwait(false);

        var belief = beliefResult.IsSuccess
            ? beliefResult.Value
            : AgentBelief.CreatePrior(agentId, taskCategory);

        // Apply outcome with partial credit support
        var updatedBelief = belief.WithOutcome(outcome);

        // Save the updated belief
        return await _beliefStore.SaveBeliefAsync(updatedBelief, cancellationToken)
            .ConfigureAwait(false);
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="shape"/> is not finite and positive.
    /// </exception>
    private double SampleGamma(double shape)
    {
        if (!double.IsFinite(shape) || shape <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(shape),
                shape,
                "Belief parameters must be finite and positive.");
        }

        // For shape less than 1, boost once: shape + 1 is >= 1, so no further boosting is needed
        if (shape < 1)
        {
            return SampleGammaMarsagliaTsang(shape + 1) * Math.Pow(_random.NextDouble(), 1.0 / shape);
        }

        return SampleGammaMarsagliaTsang(shape);
    }

    /// <summary>
    /// Samples from a Gamma(shape) distribution with scale=1 using Marsaglia and Tsang's method.
    /// </summary>
    /// <param name="shape">The shape parameter (α), which must be finite and at least 1.</param>
    /// <returns>A sample from Gamma(shape, 1).</returns>
    private double SampleGammaMarsagliaTsang(double shape)
    {
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
