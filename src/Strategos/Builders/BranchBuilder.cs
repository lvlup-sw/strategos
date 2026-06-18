// =============================================================================
// <copyright file="BranchBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the branch path builder.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class BranchBuilder<TState> : IBranchBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly List<StepDefinition> _steps = [];
    private ApprovalDefinition? _approval;

    /// <summary>
    /// Gets the steps in this branch path.
    /// </summary>
    internal IReadOnlyList<StepDefinition> Steps => _steps;

    /// <summary>
    /// Gets a value indicating whether this branch terminates without rejoining.
    /// </summary>
    internal bool IsTerminal { get; private set; }

    /// <summary>
    /// Gets the approval definition for this branch path, if any.
    /// </summary>
    internal ApprovalDefinition? Approval => _approval;

    /// <inheritdoc/>
    public IBranchBuilder<TState> Then<TStep>()
        where TStep : class, IWorkflowStep<TState>
    {
        var step = StepDefinition.Create(typeof(TStep));
        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public IBranchBuilder<TState> Then<TStep>(string instanceName)
        where TStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        var step = StepDefinition.Create(typeof(TStep), customName: null, instanceName: instanceName);
        _steps.Add(step);
        return this;
    }

    /// <inheritdoc/>
    public IBranchBuilder<TState> Then<TStep>(Action<IStepConfiguration<TState>> configure)
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
    public void Complete()
    {
        IsTerminal = true;
    }

    /// <inheritdoc/>
    public IBranchBuilder<TState> AwaitApproval<TApprover>(
        Action<IApprovalBuilder<TState, TApprover>> configure)
        where TApprover : class
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        // Determine the preceding step - either the last step in this branch, or generate a placeholder ID
        var precedingStepId = _steps.Count > 0
            ? _steps[^1].StepId
            : $"branch-entry-{Guid.NewGuid():N}";

        var builder = new ApprovalBuilder<TState, TApprover>(precedingStepId);
        configure(builder);
        _approval = builder.Build();

        return this;
    }
}
