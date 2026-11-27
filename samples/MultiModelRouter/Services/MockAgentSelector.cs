// =============================================================================
// <copyright file="MockAgentSelector.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Collections.Concurrent;
using Strategos.Abstractions;
using Strategos.Primitives;
using Strategos.Selection;

namespace MultiModelRouter.Services;

/// <summary>
/// Mock agent selector that implements Thompson Sampling with in-memory beliefs.
/// </summary>
/// <remarks>
/// <para>
/// This selector maintains Beta distribution beliefs for each (model, category) pair
/// and uses Thompson Sampling to select the best model:
/// <list type="bullet">
///   <item><description>Sample theta from Beta(alpha, beta) for each model</description></item>
///   <item><description>Select model with highest sampled theta</description></item>
///   <item><description>Update beliefs based on success/failure outcomes</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class MockAgentSelector : IAgentSelector
{
    private readonly ConcurrentDictionary<string, (int Alpha, int Beta)> _beliefs = new();
    private readonly Random _random;
    private readonly int _priorAlpha;
    private readonly int _priorBeta;

    /// <summary>
    /// Event raised when beliefs are updated.
    /// </summary>
    public event Action<string, string, bool, int, int>? BeliefUpdated;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockAgentSelector"/> class.
    /// </summary>
    /// <param name="priorAlpha">Initial alpha (successes) for all beliefs.</param>
    /// <param name="priorBeta">Initial beta (failures) for all beliefs.</param>
    /// <param name="seed">Optional random seed for reproducibility.</param>
    public MockAgentSelector(int priorAlpha = 2, int priorBeta = 2, int? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(priorAlpha, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(priorBeta, 0);

        _priorAlpha = priorAlpha;
        _priorBeta = priorBeta;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc/>
    public Task<Result<AgentSelection>> SelectAgentAsync(
        AgentSelectionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AvailableAgents.Count == 0)
        {
            return Task.FromResult(Result<AgentSelection>.Failure(new Error("NO_AGENTS", "No available agents")));
        }

        // Classify task
        var taskCategory = ClassifyTask(context.TaskDescription);

        // Sample from each agent's Beta distribution
        var bestAgentId = context.AvailableAgents[0];
        var bestTheta = double.MinValue;
        var bestObservations = 0;

        foreach (var agentId in context.AvailableAgents)
        {
            if (context.ExcludedAgents?.Contains(agentId) == true)
            {
                continue;
            }

            var key = GetBeliefKey(agentId, taskCategory.ToString());
            var (alpha, beta) = _beliefs.GetOrAdd(key, _ => (_priorAlpha, _priorBeta));

            var theta = SampleBeta(alpha, beta);
            var observations = alpha + beta - _priorAlpha - _priorBeta;

            if (theta > bestTheta)
            {
                bestTheta = theta;
                bestAgentId = agentId;
                bestObservations = observations;
            }
        }

        // Check if all agents were excluded
        if (bestTheta == double.MinValue)
        {
            return Task.FromResult(Result<AgentSelection>.Failure(
                new Error("NO_ELIGIBLE_AGENTS", "All available agents are excluded")));
        }

        // Compute confidence based on observation count
        var confidence = Math.Min(1.0, bestObservations / 20.0);

        var selection = new AgentSelection
        {
            SelectedAgentId = bestAgentId,
            TaskCategory = taskCategory,
            SampledTheta = bestTheta,
            SelectionConfidence = confidence,
        };

        return Task.FromResult(Result<AgentSelection>.Success(selection));
    }

    /// <inheritdoc/>
    public Task<Result<Unit>> RecordOutcomeAsync(
        string agentId,
        string taskCategory,
        AgentOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(taskCategory);
        ArgumentNullException.ThrowIfNull(outcome);

        var key = GetBeliefKey(agentId, taskCategory);

        _beliefs.AddOrUpdate(
            key,
            _ => outcome.Success ? (_priorAlpha + 1, _priorBeta) : (_priorAlpha, _priorBeta + 1),
            (_, current) => outcome.Success ? (current.Alpha + 1, current.Beta) : (current.Alpha, current.Beta + 1));

        var (alpha, beta) = _beliefs[key];
        BeliefUpdated?.Invoke(agentId, taskCategory, outcome.Success, alpha, beta);

        return Task.FromResult(Result<Unit>.Success(Unit.Value));
    }

    /// <summary>
    /// Gets the current beliefs for all models.
    /// </summary>
    /// <returns>A dictionary of belief keys to (alpha, beta) tuples.</returns>
    public IReadOnlyDictionary<string, (int Alpha, int Beta)> GetBeliefs()
    {
        return new Dictionary<string, (int, int)>(_beliefs);
    }

    /// <summary>
    /// Gets the success rate for a specific model and category.
    /// </summary>
    /// <param name="agentId">The model identifier.</param>
    /// <param name="taskCategory">The task category.</param>
    /// <returns>The estimated success rate (0.0 to 1.0).</returns>
    public double GetSuccessRate(string agentId, string taskCategory)
    {
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(taskCategory);

        var key = GetBeliefKey(agentId, taskCategory);
        if (_beliefs.TryGetValue(key, out var belief))
        {
            return (double)belief.Alpha / (belief.Alpha + belief.Beta);
        }

        return (double)_priorAlpha / (_priorAlpha + _priorBeta);
    }

    private static string GetBeliefKey(string agentId, string category)
    {
        return $"{agentId}:{category}";
    }

    private static TaskCategory ClassifyTask(string description)
    {
        var lower = description.ToLowerInvariant();

        if (lower.Contains("code") || lower.Contains("implement") || lower.Contains("algorithm"))
        {
            return TaskCategory.CodeGeneration;
        }

        if (lower.Contains("analyze") || lower.Contains("data"))
        {
            return TaskCategory.DataAnalysis;
        }

        if (lower.Contains("write") || lower.Contains("poem") || lower.Contains("story"))
        {
            return TaskCategory.TextGeneration;
        }

        return TaskCategory.General;
    }

    private double SampleBeta(int alpha, int beta)
    {
        // Use gamma sampling method for Beta distribution
        var x = SampleGamma(alpha);
        var y = SampleGamma(beta);

        if (x + y < double.Epsilon)
        {
            return 0.5;
        }

        return x / (x + y);
    }

    private double SampleGamma(double shape)
    {
        if (!double.IsFinite(shape) || shape <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(shape), shape, "Belief parameters must be finite and positive.");
        }

        if (shape < 1)
        {
            return SampleGammaMarsagliaTsang(shape + 1) * Math.Pow(_random.NextDouble(), 1.0 / shape);
        }

        return SampleGammaMarsagliaTsang(shape);
    }

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
                x = SampleNormal();
                v = 1.0 + (c * x);
            }
            while (v <= 0);

            v = v * v * v;
            var u = _random.NextDouble();

            if (u < 1.0 - (0.0331 * x * x * x * x))
            {
                return d * v;
            }

            if (Math.Log(u) < (0.5 * x * x) + (d * (1.0 - v + Math.Log(v))))
            {
                return d * v;
            }
        }
    }

    private double SampleNormal()
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
