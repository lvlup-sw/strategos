// =============================================================================
// <copyright file="ForkPathBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the fork path builder.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class ForkPathBuilder<TState> : IForkPathBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly List<StepDefinition> _steps = [];
    private FailureHandlerDefinition? _failureHandler;

    /// <summary>
    /// Gets the steps in this fork path.
    /// </summary>
    internal IReadOnlyList<StepDefinition> Steps => _steps;

    /// <summary>
    /// Gets the failure handler for this fork path.
    /// </summary>
    internal FailureHandlerDefinition? FailureHandler => _failureHandler;

    /// <inheritdoc/>
    public IForkPathBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        var step = StepDefinition.Create(typeof(TStep));
        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public IForkPathBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        var step = StepDefinition.Create(typeof(TStep), customName: null, instanceName: instanceName);
        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public IForkPathBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        // Build the step configuration
        var configBuilder = new StepConfigurationBuilder<TState>();
        configure(configBuilder);

        // Create step with configuration
        var step = StepDefinition.Create(typeof(TStep))
            .WithConfiguration(configBuilder.Configuration);

        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public IForkPathBuilder<TState> OnFailure(Action<IFailureBuilder<TState>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler, nameof(handler));

        // Build the failure handler path
        var failureBuilder = new FailureBuilder<TState>();
        handler(failureBuilder);

        // Create the failure handler definition
        _failureHandler = FailureHandlerDefinition.Create(
            FailureHandlerScope.ForkPath,
            failureBuilder.Steps,
            failureBuilder.IsTerminal);

        return this;
    }
}
