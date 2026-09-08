// =============================================================================
// <copyright file="ForkJoinBuilder.cs" company="Levelup Software">
// Copyright (c) Levelup Software. All rights reserved.
// </copyright>
// =============================================================================

namespace Strategos.Builders;

/// <summary>
/// Internal implementation of the fork/join builder.
/// </summary>
/// <typeparam name="TState">The workflow state type.</typeparam>
internal sealed class ForkJoinBuilder<TState> : IForkJoinBuilder<TState>
    where TState : class, IWorkflowState
{
    private readonly WorkflowBuilder<TState> _workflowBuilder;
    private readonly ForkPointDefinition _pendingForkPoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForkJoinBuilder{TState}"/> class.
    /// </summary>
    /// <param name="workflowBuilder">The parent workflow builder.</param>
    /// <param name="pendingForkPoint">The fork point awaiting a join step.</param>
    internal ForkJoinBuilder(
        WorkflowBuilder<TState> workflowBuilder,
        ForkPointDefinition pendingForkPoint)
    {
        _workflowBuilder = workflowBuilder;
        _pendingForkPoint = pendingForkPoint;
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Join<TJoinStep>()
        where TJoinStep : class, IWorkflowStep<TState>
    {
        return CompleteJoin(StepDefinition.Create(typeof(TJoinStep)));
    }

    /// <inheritdoc/>
    public IWorkflowBuilder<TState> Join<TJoinStep>(Action<IStepConfiguration<TState>> configure)
        where TJoinStep : class, IWorkflowStep<TState>
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        var configuration = new StepConfigurationBuilder<TState>();
        configure(configuration);
        return CompleteJoin(configuration.ApplyTo(StepDefinition.Create(typeof(TJoinStep))));
    }

    private IWorkflowBuilder<TState> CompleteJoin(StepDefinition joinStep)
    {
        // Complete the fork point with the join step ID
        var completedForkPoint = _pendingForkPoint with { JoinStepId = joinStep.StepId };

        // Register the fork point and join step with the workflow builder
        _workflowBuilder.CompleteForkJoin(completedForkPoint, joinStep);

        return _workflowBuilder;
    }
}
