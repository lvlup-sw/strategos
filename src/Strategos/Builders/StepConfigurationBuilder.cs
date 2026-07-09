// =============================================================================
// <copyright file="StepConfigurationBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

using System.Linq.Expressions;

using Strategos.Definitions;

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the step configuration builder.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class StepConfigurationBuilder<TState> : IStepConfiguration<TState>
    where TState : class, IWorkflowState
{
    private StepConfigurationDefinition _configuration = StepConfigurationDefinition.Empty;

    /// <summary>
    /// Gets the built step configuration definition.
    /// </summary>
    internal StepConfigurationDefinition Configuration => _configuration;

    /// <inheritdoc/>
    public IStepConfiguration<TState> RequireConfidence(double threshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 0.0, nameof(threshold));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, 1.0, nameof(threshold));

        _configuration = _configuration with { ConfidenceThreshold = threshold };
        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> OnLowConfidence(Action<IBranchBuilder<TState>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        // Build the handler path using BranchBuilder
        var branchBuilder = new BranchBuilder<TState>();
        handler(branchBuilder);

        var handlerDefinition = LowConfidenceHandlerDefinition.Create(branchBuilder.Steps);
        _configuration = _configuration.WithLowConfidenceHandler(handlerDefinition);

        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> Compensate<TCompensation>()
        where TCompensation : class, IWorkflowStep<TState>
    {
        var compensation = CompensationConfiguration.Create<TCompensation>();
        _configuration = _configuration.WithCompensation(compensation);
        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> WithRetry(int maxAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1, nameof(maxAttempts));

        var retry = RetryConfiguration.Create(maxAttempts);
        _configuration = _configuration.WithRetry(retry);
        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> WithRetry(int maxAttempts, TimeSpan initialDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1, nameof(maxAttempts));

        var retry = RetryConfiguration.WithExponentialBackoff(maxAttempts, initialDelay);
        _configuration = _configuration.WithRetry(retry);
        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> WithTimeout(TimeSpan timeout)
    {
        _configuration = _configuration.WithTimeout(timeout);
        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> ValidateState(
        Expression<Func<TState, bool>> predicate,
        string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
        ArgumentNullException.ThrowIfNull(errorMessage, nameof(errorMessage));

        var predicateExpression = predicate.Body.ToString();
        var validation = ValidationDefinition.Create(predicateExpression, errorMessage);
        _configuration = _configuration.WithValidation(validation);
        return this;
    }

    /// <inheritdoc/>
    public IStepConfiguration<TState> WithContext(Action<IContextBuilder<TState>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        var contextBuilder = new ContextBuilder<TState>();
        configure(contextBuilder);

        _configuration = _configuration.WithContext(contextBuilder.Definition);
        return this;
    }
}
